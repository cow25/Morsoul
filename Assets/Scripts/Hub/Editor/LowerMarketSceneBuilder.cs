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
    /// Rebuilds Hub_LowerMarket and its art overlay Hub_LowerMarket_Art at 3x
    /// expanded scale using Pixel Crawler tilesets, 4 northern district buildings,
    /// market stalls, craft stations, and populated interactable + ambient strolling
    /// townsfolk while preserving all core story anchors and test contracts.
    /// </summary>
    public static class LowerMarketSceneBuilder
    {
        const string MainScenePath = "Assets/Scenes/Hub_LowerMarket.unity";
        const string ArtScenePath = "Assets/Scenes/Hub_LowerMarket_Art.unity";
        const string BarksPath = "Assets/Data/Dialogue/Case01/D_MarketBarks_01.asset";
        const string FlavorDialogueFolder = "Assets/Data/Dialogue/Case01";

        const string RoofsPath = "Assets/Art/Environments/Buildings/Exterior/Roofs.png";
        const string ExtPropsPath = "Assets/Art/Environments/Buildings/Exterior/Props.png";
        const string FurniturePath = "Assets/Art/Environments/Props/Static/Furniture.png";
        const string FarmPath = "Assets/Art/Environments/Props/Static/Farm.png";
        const string VegPath = "Assets/Art/Environments/Props/Static/Vegetation.png";
        const string RocksPath = "Assets/Art/Environments/Props/Static/Rocks.png";
        const string DungeonPropsPath = "Assets/Art/Environments/Props/Static/Dungeon_Props.png";
        const string ResourcesPath = "Assets/Art/Environments/Props/Static/Resources.png";
        const string EsotericPath = "Assets/Art/Environments/Props/Static/Esoteric.png";
        const string MeatPath = "Assets/Art/Environments/Props/Static/Meat.png";
        const string PanPath = "Assets/Art/Environments/Props/Static/Pan.png";
        const string FurnacePath = "Assets/Art/Environments/Stations/Furnace/Furnace.png";
        const string BonfirePath = "Assets/Art/Environments/Stations/Bonfire/Bonfire.png";
        const string CookerPath = "Assets/Art/Environments/Stations/Cooking/Cooker/Cooker_01.png";
        const string SawmillPath = "Assets/Art/Environments/Stations/Sawmill/Level_1.png";
        const string IntPropsPath = "Assets/Art/Environments/Buildings/Interior/Interior_Props_01.png";
        const string TreeM1Path = "Assets/Art/Environments/Props/Trees/Model_01/Size_03.png";
        const string TreeM2Path = "Assets/Art/Environments/Props/Trees/Model_02/Size_03.png";

        // Newly integrated asset packs (Fantasy Tileset, Village Interiors, Dungeon Objects, Retro Interior)
        const string FantasyBuildingsDir = "Assets/Art/Environments/Fantasy_Tileset/Buildings/";
        const string FantasyPropsDir = "Assets/Art/Environments/Fantasy_Tileset/Props/";
        const string VillagePropsDir = "Assets/Art/Environments/Village_Interiors/furniture_and_props_sprites/";
        const string SuppliesSheetPath = "Assets/Art/Environments/Dungeon_Objects/supplies_objects.png";
        const string OtherObjectsSheetPath = "Assets/Art/Environments/Dungeon_Objects/Other_objects.png";
        const string RetroItemsPath = "Assets/Art/Environments/Retro_Interior/TopDownHouse_SmallItems.png";

        struct NpcProfiles
        {
            public VisualProfile Eliza;
            public VisualProfile PeasantF;
            public VisualProfile Ignatius;
            public VisualProfile Ovan;
            public VisualProfile Carrier;
            public VisualProfile GuardA;
            public VisualProfile MerchantA;
            public VisualProfile AdventurerA;
            public VisualProfile VillagerA;
            public VisualProfile MerchantB;
            public VisualProfile SmithA;
            public VisualProfile TavernA;
            public VisualProfile TavernB;
        }

        [MenuItem("Morsoul/Scenes/Lower Market", priority = 13)]
        public static void RebuildMenu() => Rebuild();

        public static void Rebuild()
        {
            GuildIntroContentGenerator.Generate();
            MarketPackAssetSetup.EnsureAll();
            PixelCrawlerHubToolkit.ClearCache();
            RebuildMainSceneBindings();
            AssetDatabase.SaveAssets();
            Debug.Log("LowerMarketSceneBuilder: rebuilt unified 3x expanded Hub_LowerMarket.");
        }

        static Sprite LoadPackSprite(string assetPath)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sp != null) return sp;
            foreach (var obj in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
            {
                if (obj is Sprite sub) return sub;
            }
            return null;
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

            // Wide grass carpet covering expanded 56x36 hub + ultrawide camera overshoot bounds.
            PixelCrawlerHubToolkit.FillRect(grassMap, grassTile, -40, -26, 40, 26);

            // Dirt roads: West Arrival Road, South Gate Boulevard, Vault Alley, and East Market perimeter.
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -36, -2, -8, 1);
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -2, -24, 2, -8);
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -12, 9, -9, 13); // Vault Alley off Shop District
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, 10, -8, 25, -6);
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, 10, 7, 25, 9);

            // --- Zone B: Central Park / Public Area (cross paths + central well plaza, leaving 4 green quadrants) ---
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -4, -4, 4, 4);    // Central Well Plaza
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -2, -8, 2, 10);   // North-South Park Avenue
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -9, -1, 10, 1);   // East-West Park Spine
            // Outer park ring path
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -8, 7, 8, 8);
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -8, -8, 8, -7);
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -8, -7, -7, 7);
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, 7, -7, 8, 7);

            // --- Zone C: Investigator's Guild Forecourt (North Anchor) ---
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -7, 9, 7, 13);

            // --- Zone A: Shop District Street & Pads (West / Northwest) ---
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -26, 2, -10, 12);   // Upper commercial street
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -26, -12, -10, -3); // Lower blacksmith yard & future plot pad

            // --- Zone D: Vendor / Open Market Square (East / Southeast) ---
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, 10, -6, 25, 7);

            // Town Brook wrapping the southeastern quarter safely off the paved bazaar.
            PixelCrawlerHubToolkit.FillRect(waterMap, waterTile, 18, -22, 34, -11);
            PixelCrawlerHubToolkit.FillRect(waterMap, waterTile, 26, -11, 34, -5);

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

            // --- Zone C: Investigator's Guild Hall (Grand North-Center Anchor, x=-5..5, y=13..16) ---
            PaintWallFacade(wallsMap, xStart: -5, width: 11, yBottom: 13, kitColOffset: 30);
            // Fill the gable peak behind Guild_Roof with cream stucco (Walls_13_32) so no gap shows under the eaves.
            var guildStuccoTile = PixelCrawlerHubToolkit.LoadStaticTile("Exterior_Walls", "Walls_13_32");
            if (guildStuccoTile != null)
            {
                for (int gx = -4; gx <= 4; gx++)
                {
                    wallsMap.SetTile(new Vector3Int(gx, 16, 0), guildStuccoTile);
                    wallsMap.SetTile(new Vector3Int(gx, 17, 0), guildStuccoTile);
                    if (gx >= -2 && gx <= 2)
                        wallsMap.SetTile(new Vector3Int(gx, 18, 0), guildStuccoTile);
                }
            }

            wallsMap.RefreshAllTiles();

            var roofBrown = PixelCrawlerHubToolkit.LoadSprite(RoofsPath, "Ext_Roofs_00");
            var roofGreen = PixelCrawlerHubToolkit.LoadSprite(RoofsPath, "Ext_Roofs_01");

            // Guild Hall wide single green roof (lowered slightly so eaves meet the upper facade cleanly with zero gap)
            var guildRoof = PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Roof",
                roofGreen ?? roofBrown, new Vector2(0.0f, 14.45f), zBias: -0.45f, sortingOrder: -1);
            if (guildRoof != null)
                guildRoof.transform.localScale = new Vector3(1.55f, 1.05f, 1f);

            // --- Zone A: Shop District Permanent Buildings (West / Northwest) ---
            // 1. Apothecary & Herbalist (House_Hay_3: 11.25x8.0 complete two-story stone/flowerbox building)
            var apothBuilding = LoadPackSprite(FantasyBuildingsDir + "House_Hay_3.png");
            if (apothBuilding != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Shop_Apothecary",
                    apothBuilding, new Vector2(-22.5f, 12.8f), zBias: -0.005f, sortingOrder: 0,
                    colliderSize: new Vector2(10.6f, 7.2f), colliderOffset: new Vector2(0f, 3.6f));
            }

            // 2. Ignatius's Alchemical Workshop (House_Hay_1: 5.5x6.5 stone workshop adjacent to Vault Alley)
            var vaultBuilding = LoadPackSprite(FantasyBuildingsDir + "House_Hay_1.png");
            if (vaultBuilding != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Shop_IgnatiusWorkshop",
                    vaultBuilding, new Vector2(-14.5f, 12.8f), zBias: -0.005f, sortingOrder: 0,
                    colliderSize: new Vector2(5.2f, 5.8f), colliderOffset: new Vector2(0f, 2.9f));
            }

            // 3. General Store / Bakery (House_Hay_4_Purple: 8.0x8.0 storefront facing West Road at y=2.5)
            var marketShopBuilding = LoadPackSprite(FantasyBuildingsDir + "House_Hay_4_Purple.png");
            if (marketShopBuilding != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Shop_GeneralStore",
                    marketShopBuilding, new Vector2(-22.5f, 2.5f), zBias: -0.005f, sortingOrder: 0,
                    colliderSize: new Vector2(7.6f, 7.2f), colliderOffset: new Vector2(0f, 3.6f));
            }

            // 4. Blacksmith & Armory (House_Hay_2: 9.81x7.0 stone/timber building at y=-8.2)
            var smithBuilding = LoadPackSprite(FantasyBuildingsDir + "House_Hay_2.png");
            if (smithBuilding != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Shop_Blacksmith",
                    smithBuilding, new Vector2(-15.0f, -8.2f), zBias: -0.005f, sortingOrder: 0,
                    colliderSize: new Vector2(9.4f, 6.4f), colliderOffset: new Vector2(0f, 3.2f));
            }

            var guildArchDoor = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_00");
            var windowShuttered = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_08");
            var windowTall = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_11");
            var flowerBox = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_12");

            // General Storefront produce baskets
            var basketCitrus = LoadPackSprite(VillagePropsDir + "kitchen_props4_2.png");
            var basketApples = LoadPackSprite(VillagePropsDir + "kitchen_props4_3.png");
            if (basketCitrus != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Storefront_Basket_A", basketCitrus,
                    new Vector2(-24.8f, 2.2f), colliderSize: new Vector2(0.85f, 0.55f), colliderOffset: new Vector2(0f, 0.25f));
            if (basketApples != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Storefront_Basket_B", basketApples,
                    new Vector2(-23.8f, 2.2f), colliderSize: new Vector2(0.85f, 0.55f), colliderOffset: new Vector2(0f, 0.25f));

            // Investigator's Guild Hall facade — arched door centered at x=0, y=13.0 behind GuildEntrance.
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Door", guildArchDoor, new Vector2(0.0f, 13.0f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Win_W1", windowTall ?? windowShuttered, new Vector2(-3.8f, 13.55f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Win_W2", windowTall ?? windowShuttered, new Vector2(-2.2f, 13.55f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Win_E1", windowTall ?? windowShuttered, new Vector2(2.2f, 13.55f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Win_E2", windowTall ?? windowShuttered, new Vector2(3.8f, 13.55f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Flower_W", flowerBox, new Vector2(-3.8f, 13.0f), zBias: 0.24f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Flower_E", flowerBox, new Vector2(3.8f, 13.0f), zBias: 0.24f);
        }

        static void PaintWallFacade(Tilemap wallsMap, int xStart, int width, int yBottom, int kitColOffset)
        {
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

            // Core terrain/nature & craft station sprites (Pixel Crawler).
            var coalPile = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_02");
            var timberLog = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_21");
            var brickSmelter = PixelCrawlerHubToolkit.LoadSprite(FurnacePath, "Furnace_09");
            var stoneFurnace = PixelCrawlerHubToolkit.LoadSprite(FurnacePath, "Furnace_15");
            var sawmill = PixelCrawlerHubToolkit.LoadSprite(SawmillPath, "Level_1_0");
            var oakGreen = PixelCrawlerHubToolkit.LoadSprite(TreeM1Path, "Tree_Model_01_Size_03_00");
            var oakGold = PixelCrawlerHubToolkit.LoadSprite(TreeM1Path, "Tree_Model_01_Size_03_01");
            var pineA = PixelCrawlerHubToolkit.LoadSprite(TreeM2Path, "Tree_Model_02_Size_03_00");
            var pineB = PixelCrawlerHubToolkit.LoadSprite(TreeM2Path, "Tree_Model_02_Size_03_01");
            var bushMed = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_04");
            var bushSmall = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_12");
            var reedTall = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_33");
            var flowerA = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_34");
            var flowerCluster = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_147");
            var boulder = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_05");
            var blueCrystal = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_101");

            // Fantasy Tileset props & landmarks.
            var lampDouble = LoadPackSprite(FantasyPropsDir + "LampPost_3.png");
            var cityGateArch = LoadPackSprite(FantasyBuildingsDir + "CityWall_Gate_1.png");
            var purpleBanner = LoadPackSprite(FantasyPropsDir + "Banner_Stick_1_Purple.png");
            var marketTable = LoadPackSprite(FantasyPropsDir + "Table_Medium_1.png");
            var smallBench = LoadPackSprite(FantasyPropsDir + "Bench_3.png");
            var signRight = LoadPackSprite(FantasyPropsDir + "Sign_1.png");
            var signLeft = LoadPackSprite(FantasyPropsDir + "Sign_2.png");

            // Village Interiors market stalls, shelves, produce baskets & clusters.
            var stallStandRight = LoadPackSprite(VillagePropsDir + "stand-1.png");
            var stallStandLeft = LoadPackSprite(VillagePropsDir + "stand-2.png");
            var apothShelfA = LoadPackSprite(VillagePropsDir + "storage_countertop_0.png");
            var apothShelfB = LoadPackSprite(VillagePropsDir + "storage_countertop_1.png");
            var roastRack = LoadPackSprite(VillagePropsDir + "kitchen_props6_0.png");
            var watermelon = LoadPackSprite(VillagePropsDir + "kitchen_props4_0.png");
            var potatoBasket = LoadPackSprite(VillagePropsDir + "kitchen_props4_1.png");
            var fruitBasket = LoadPackSprite(VillagePropsDir + "kitchen_props4_2.png");
            var appleBasket = LoadPackSprite(VillagePropsDir + "kitchen_props4_3.png");
            var redAppleBasket = LoadPackSprite(VillagePropsDir + "kitchen_props4_7.png");
            var breadBasket = LoadPackSprite(VillagePropsDir + "kitchen_props4_9.png");
            var flourCrates = LoadPackSprite(VillagePropsDir + "crates_supply_0.png");
            var grainCrates = LoadPackSprite(VillagePropsDir + "crates_supply_2.png");
            var sackWide = LoadPackSprite(VillagePropsDir + "supply_bags_0.png");
            var grainSackStack = LoadPackSprite(VillagePropsDir + "supply_bags_1.png");
            var barrelGroup = LoadPackSprite(VillagePropsDir + "barrels3_0.png");
            var singleBarrel = LoadPackSprite(VillagePropsDir + "barrels_0.png");

            // Dungeon Objects market racks, weapon/armor racks & heraldic banners.
            var rackProduceA = PixelCrawlerHubToolkit.LoadSprite(SuppliesSheetPath, "supplies_objects_00");
            var rackProduceB = PixelCrawlerHubToolkit.LoadSprite(SuppliesSheetPath, "supplies_objects_01");
            var rackProduceC = PixelCrawlerHubToolkit.LoadSprite(SuppliesSheetPath, "supplies_objects_02");
            var rackHerbsA = PixelCrawlerHubToolkit.LoadSprite(SuppliesSheetPath, "supplies_objects_03");
            var herbCrate = PixelCrawlerHubToolkit.LoadSprite(SuppliesSheetPath, "supplies_objects_19");
            var weaponRackA = PixelCrawlerHubToolkit.LoadSprite(SuppliesSheetPath, "supplies_objects_76");
            var weaponRackB = PixelCrawlerHubToolkit.LoadSprite(SuppliesSheetPath, "supplies_objects_77");
            var ciderBarrel = PixelCrawlerHubToolkit.LoadSprite(OtherObjectsSheetPath, "Other_objects_10");
            var aleKeg = PixelCrawlerHubToolkit.LoadSprite(OtherObjectsSheetPath, "Other_objects_11");
            var armorStandA = PixelCrawlerHubToolkit.LoadSprite(OtherObjectsSheetPath, "Other_objects_12");
            var armorStandB = PixelCrawlerHubToolkit.LoadSprite(OtherObjectsSheetPath, "Other_objects_13");
            var bannerRed = PixelCrawlerHubToolkit.LoadSprite(OtherObjectsSheetPath, "Other_objects_15");
            var bannerBlue = PixelCrawlerHubToolkit.LoadSprite(OtherObjectsSheetPath, "Other_objects_16");

            // Retro Interior tabletop accents (proportionally matched at 18 PPU).
            var openBook = PixelCrawlerHubToolkit.LoadSprite(RetroItemsPath, "RetroItem_24");
            var litCandle = PixelCrawlerHubToolkit.LoadSprite(RetroItemsPath, "RetroItem_26");
            var foodPlate = PixelCrawlerHubToolkit.LoadSprite(RetroItemsPath, "RetroItem_32");
            var stewPot = PixelCrawlerHubToolkit.LoadSprite(RetroItemsPath, "RetroItem_38");
            var bakedPie = PixelCrawlerHubToolkit.LoadSprite(RetroItemsPath, "RetroItem_61");

            // --- Zone B: Central Park / Public Area (Well_Hay_1 centerpiece at 0,-0.3, benches, trees, flowers) ---
            var townWell = LoadPackSprite(FantasyBuildingsDir + "Well_Hay_1.png");
            if (townWell != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Park_TownWell", townWell,
                    new Vector2(0.0f, -0.3f), colliderSize: new Vector2(2.2f, 1.3f), colliderOffset: new Vector2(0f, 0.65f));
            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "Plaza_WarmGlow_Center", new Vector2(0f, 0.5f), new Color(1.0f, 0.80f, 0.45f, 0.22f), scale: 5.5f);
            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "Plaza_WarmGlow_Guild", new Vector2(0f, 11.2f), new Color(1.0f, 0.80f, 0.45f, 0.22f), scale: 5.0f);
            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "Plaza_WarmGlow_Shop", new Vector2(-17.5f, 4.5f), new Color(1.0f, 0.74f, 0.38f, 0.20f), scale: 4.5f);
            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "Plaza_WarmGlow_Market", new Vector2(16.5f, 0.5f), new Color(1.0f, 0.72f, 0.34f, 0.22f), scale: 5.0f);

            // Park Benches in NE, SW, SE quadrants (RestSpot occupies the NW quadrant at -5.2, 4.2)
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Park_Bench_NE", smallBench,
                new Vector2(5.2f, 4.2f), colliderSize: new Vector2(1.8f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Park_Bench_SW", smallBench,
                new Vector2(-5.2f, -4.8f), colliderSize: new Vector2(1.8f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Park_Bench_SE", smallBench,
                new Vector2(5.2f, -4.8f), colliderSize: new Vector2(1.8f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));

            // --- Zone C: Investigator's Guild Forecourt Banners ---
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Guild_StandBanner_W", purpleBanner, new Vector2(-2.2f, 12.1f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Guild_StandBanner_E", purpleBanner, new Vector2(2.2f, 12.1f));

            // --- Zone A: Shop District Props (West / Northwest) ---
            // West Arrival Caravan Staging (x = -26.8..-24.5, y = -1.5)
            var carrierWagon = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/Carrier_Wagon.png", 16f);
            if (carrierWagon != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Carrier_Wagon", carrierWagon,
                    new Vector2(-26.8f, -1.6f), colliderSize: new Vector2(4.2f, 1.4f), colliderOffset: new Vector2(0f, 0.7f));

            // Apothecary exterior props (NW-West, x = -26..-20, cleanly in front of House_Hay_3)
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Apoth_Shelf_A", apothShelfA,
                new Vector2(-25.8f, 11.6f), colliderSize: new Vector2(1.8f, 0.85f), colliderOffset: new Vector2(0f, 0.42f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Apoth_HerbCrate", herbCrate,
                new Vector2(-25.8f, 10.4f), colliderSize: new Vector2(0.9f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
            var apothTable = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Apoth_Table", marketTable,
                new Vector2(-22.2f, 10.2f), colliderSize: new Vector2(2.1f, 1.25f), colliderOffset: new Vector2(0f, 0.65f));
            PlaceTabletopProp(apothTable, "Apoth_Book", openBook, new Vector2(-0.35f, 0.65f));
            PlaceTabletopProp(apothTable, "Apoth_Candle", litCandle, new Vector2(0.35f, 0.68f));

            // Ignatius's Alchemical Workshop & Vault Alley accents (NW-East, x = -18..-9)
            if (blueCrystal != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Vault_CrystalAccent", blueCrystal, new Vector2(-11.8f, 12.4f));

            // Blacksmith & Armory Yard (SW, courtyard in front of building at y = -11.5..-8.3)
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Forge_Blast", stoneFurnace,
                new Vector2(-19.8f, -9.6f), colliderSize: new Vector2(1.8f, 1.2f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Forge_CoalPile", coalPile,
                new Vector2(-19.8f, -11.2f), colliderSize: new Vector2(1.1f, 0.65f), colliderOffset: new Vector2(0f, 0.32f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Forge_Smelter", brickSmelter,
                new Vector2(-9.6f, -9.6f), colliderSize: new Vector2(2.2f, 1.3f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Armory_Rack_A", weaponRackA,
                new Vector2(-17.8f, -11.4f), colliderSize: new Vector2(2.1f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Armory_Rack_B", weaponRackB,
                new Vector2(-12.2f, -11.4f), colliderSize: new Vector2(2.1f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Armory_Armor_W", armorStandA,
                new Vector2(-17.4f, -8.5f), colliderSize: new Vector2(0.85f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Armory_Armor_E", armorStandB,
                new Vector2(-12.6f, -8.5f), colliderSize: new Vector2(0.85f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Forge_QuenchBarrel", singleBarrel,
                new Vector2(-10.2f, -10.4f), colliderSize: new Vector2(0.85f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Forge_CiderCask", ciderBarrel,
                new Vector2(-10.2f, -11.4f), colliderSize: new Vector2(1.0f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));

            // --- Zone D: Vendor / Open Market (East / Southeast: Two Parallel Trading Lanes) ---
            // North Trading Lane — Produce & Dry Goods (y = 3.5..6.5)
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_West_Counter", stallStandLeft,
                new Vector2(15.5f, 4.6f), colliderSize: new Vector2(3.6f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Rack_W0", rackProduceA,
                new Vector2(12.5f, 6.2f), colliderSize: new Vector2(2.0f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Rack_W1", rackProduceB,
                new Vector2(15.2f, 6.2f), colliderSize: new Vector2(2.0f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Rack_W2", rackProduceC,
                new Vector2(17.9f, 6.2f), colliderSize: new Vector2(2.0f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));

            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Basket_Citrus", fruitBasket,
                new Vector2(13.2f, 4.4f), colliderSize: new Vector2(0.8f, 0.55f), colliderOffset: new Vector2(0f, 0.28f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Basket_Apples", appleBasket,
                new Vector2(14.1f, 4.35f), colliderSize: new Vector2(0.8f, 0.55f), colliderOffset: new Vector2(0f, 0.28f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Basket_Red", redAppleBasket,
                new Vector2(17.0f, 4.4f), colliderSize: new Vector2(0.8f, 0.55f), colliderOffset: new Vector2(0f, 0.28f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Basket_Potato", potatoBasket,
                new Vector2(12.2f, 4.4f), colliderSize: new Vector2(0.8f, 0.55f), colliderOffset: new Vector2(0f, 0.28f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Watermelon", watermelon,
                new Vector2(18.0f, 4.4f), colliderSize: new Vector2(0.75f, 0.5f), colliderOffset: new Vector2(0f, 0.25f));

            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Caravan_Crates_W", flourCrates,
                new Vector2(21.2f, 5.8f), colliderSize: new Vector2(2.8f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Caravan_Barrels_W", barrelGroup,
                new Vector2(23.5f, 4.4f), colliderSize: new Vector2(1.8f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Caravan_Sacks_W", sackWide,
                new Vector2(21.5f, 4.3f), colliderSize: new Vector2(1.2f, 0.65f), colliderOffset: new Vector2(0f, 0.32f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Caravan_GrainSack_W", grainSackStack ?? sackWide,
                new Vector2(23.2f, 5.8f), colliderSize: new Vector2(1.1f, 0.65f), colliderOffset: new Vector2(0f, 0.32f));

            // South Trading Lane — Hot Food, Ale & Provisions (y = -5.5..-2.5)
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_Counter", stallStandRight,
                new Vector2(15.5f, -4.6f), colliderSize: new Vector2(2.1f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_RoastRack", roastRack,
                new Vector2(18.2f, -4.4f), colliderSize: new Vector2(2.5f, 0.95f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_Barrels", barrelGroup,
                new Vector2(12.6f, -4.5f), colliderSize: new Vector2(1.7f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_AleKeg", aleKeg,
                new Vector2(11.0f, -4.5f), colliderSize: new Vector2(1.4f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_BreadBasket", breadBasket,
                new Vector2(14.2f, -4.7f), colliderSize: new Vector2(0.8f, 0.55f), colliderOffset: new Vector2(0f, 0.28f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_FruitBasket", fruitBasket,
                new Vector2(16.7f, -4.7f), colliderSize: new Vector2(0.8f, 0.55f), colliderOffset: new Vector2(0f, 0.28f));
            var serviceTable = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_ServiceTable", marketTable,
                new Vector2(21.4f, -3.5f), colliderSize: new Vector2(2.1f, 1.25f), colliderOffset: new Vector2(0f, 0.65f));
            PlaceTabletopProp(serviceTable, "Stall_East_Dish", foodPlate, new Vector2(-0.35f, 0.72f));
            PlaceTabletopProp(serviceTable, "Stall_East_StewPot", stewPot, new Vector2(0.4f, 0.75f));

            // --- South City Gate, Lumber Depot & Banners ---
            if (cityGateArch != null)
            {
                var gate = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "South_CityGate_Arch", cityGateArch,
                    new Vector2(0.0f, -19.5f), zBias: -0.25f, sortingOrder: 5);
                if (gate != null)
                {
                    AddGatePillarCollider(gate, "ArchPillar_W", -2.05f, 2.5f);
                    AddGatePillarCollider(gate, "ArchPillar_E", 2.05f, 2.5f);
                }
            }
            if (sawmill != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "South_Sawmill", sawmill,
                    new Vector2(-9.5f, -12.5f), colliderSize: new Vector2(1.6f, 1.1f), colliderOffset: new Vector2(0f, 0.4f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "South_TimberPile", timberLog, new Vector2(-7.4f, -12.7f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "South_GrainCrates", grainCrates,
                new Vector2(7.5f, -11.5f), colliderSize: new Vector2(1.9f, 0.95f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "South_Sack", sackWide, new Vector2(9.4f, -11.6f));

            // --- Street Lamps along Avenues & Park Entrances ---
            var lampPositions = new[]
            {
                new Vector2(-5.5f, 11.2f), new Vector2(5.5f, 11.2f),   // Guild Forecourt
                new Vector2(-3.2f, 6.8f),  new Vector2(3.2f, -7.2f),   // Park N/S Entrances
                new Vector2(-8.2f, 1.8f),  new Vector2(8.2f, 1.8f),    // Park W/E Entrances
                new Vector2(-26.4f, 1.8f), new Vector2(23.5f, 1.8f),   // West Road & East Market Aisle
                new Vector2(-3.5f, -14.5f), new Vector2(3.5f, -14.5f), // South Gate Boulevard
            };
            for (int i = 0; i < lampPositions.Length; i++)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Market_Lamp_{i:D2}", lampDouble,
                    lampPositions[i], colliderSize: new Vector2(0.6f, 0.4f), colliderOffset: new Vector2(0f, 0.2f));
            }

            // --- Park & Perimeter Trees (keep roads, Vault Alley, and SE TownBrook clear) ---
            Sprite[] borderTrees = { oakGreen, pineA, oakGold, pineB };
            var treeCoords = new List<Vector2>
            {
                // Central Park 4 Green Quadrants
                new Vector2(-6.0f, 5.5f),  new Vector2(6.0f, 5.5f),
                new Vector2(-6.0f, -6.2f), new Vector2(6.0f, -6.2f),
                // West border trees (double staggered forest line, clear of West Arrival Road y = -2..2)
                new Vector2(-27.2f, 13.5f), new Vector2(-27.5f, 8.2f), new Vector2(-27.5f, 3.8f),
                new Vector2(-27.5f, -4.5f), new Vector2(-27.0f, -10.5f), new Vector2(-26.5f, -15.0f),
                new Vector2(-30.5f, 15.0f), new Vector2(-30.8f, 10.5f), new Vector2(-30.8f, 5.8f),
                new Vector2(-30.8f, -5.8f), new Vector2(-30.5f, -11.5f), new Vector2(-30.0f, -16.5f),
                // North deep forest crown behind buildings
                new Vector2(-26.0f, 16.5f), new Vector2(-18.5f, 16.2f), new Vector2(-8.5f, 16.5f),
                new Vector2(8.5f, 16.5f),   new Vector2(18.0f, 15.8f),  new Vector2(26.5f, 14.5f),
                new Vector2(-22.5f, 19.2f), new Vector2(-12.5f, 19.0f), new Vector2(-4.5f, 19.5f),
                new Vector2(4.5f, 19.5f),   new Vector2(13.5f, 18.8f),  new Vector2(22.5f, 18.5f),
                // North-East woodland pocket north of Vendor Open Market
                new Vector2(11.5f, 12.8f), new Vector2(15.2f, 11.8f), new Vector2(20.5f, 12.2f),
                new Vector2(24.5f, 11.5f), new Vector2(13.8f, 15.5f), new Vector2(22.0f, 15.0f),
                // East border trees (north of brook)
                new Vector2(27.0f, 10.0f),  new Vector2(27.2f, 4.5f),  new Vector2(27.2f, -1.5f),
                new Vector2(30.2f, 7.5f),   new Vector2(30.5f, 1.5f),  new Vector2(30.2f, -3.5f),
                // South-East Brook framing
                new Vector2(14.5f, -12.5f), new Vector2(15.2f, -16.2f), new Vector2(12.0f, -15.0f),
            };
            for (int x = -24; x <= 14; x += 4)
            {
                if (x >= -3 && x <= 3) continue; // Keep South Gate road clear
                if (x >= -11 && x <= -7) continue; // Keep South Sawmill clear
                treeCoords.Add(new Vector2(x, -17.5f - (Mathf.Abs(x) % 2) * 0.5f));
            }
            for (int i = 0; i < treeCoords.Count; i++)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Market_Tree_{i:D2}",
                    borderTrees[i % borderTrees.Length], treeCoords[i],
                    colliderSize: new Vector2(1.2f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));

            // --- Bushes, Flowers, Reeds & Boulders ---
            var bushes = new[]
            {
                // Central Park corner shrubs & inner quadrant accents
                new Vector2(-4.2f, 5.8f), new Vector2(4.2f, 5.8f),
                new Vector2(-4.2f, -6.0f), new Vector2(4.2f, -6.0f),
                new Vector2(-3.8f, 3.2f), new Vector2(3.8f, 3.2f),
                new Vector2(-3.8f, -3.5f), new Vector2(3.8f, -3.5f),
                // Perimeter & District framing
                new Vector2(-26.5f, 11.2f), new Vector2(-18.2f, 13.8f), new Vector2(-7.8f, 12.2f), new Vector2(7.8f, 12.2f),
                new Vector2(-8.8f, 14.8f),  new Vector2(8.8f, 14.8f),
                new Vector2(10.5f, 9.2f),   new Vector2(14.5f, 9.6f),   new Vector2(19.5f, 9.5f),   new Vector2(24.5f, 8.8f),
                new Vector2(-25.5f, -8.5f), new Vector2(-27.2f, 2.5f),  new Vector2(-27.2f, -2.8f),
                new Vector2(15.5f, -9.2f),  new Vector2(16.2f, -12.8f), new Vector2(16.5f, -15.2f),
                new Vector2(-5.5f, -14.5f), new Vector2(5.5f, -14.5f),  new Vector2(11.5f, -12.5f),
            };
            for (int i = 0; i < bushes.Length; i++)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Market_Bush_{i:D2}",
                    i % 3 == 0 ? bushSmall : bushMed, bushes[i]);

            var flowers = new[]
            {
                // Central Park flowerbeds
                new Vector2(-5.2f, 3.0f), new Vector2(5.2f, 3.0f),
                new Vector2(-5.2f, -3.2f), new Vector2(5.2f, -3.2f),
                new Vector2(-2.6f, 3.8f),  new Vector2(2.6f, 3.8f),
                new Vector2(-2.6f, -3.8f), new Vector2(2.6f, -3.8f),
                // Guild & Brook accents
                new Vector2(-6.2f, 10.5f), new Vector2(6.2f, 10.5f),
                new Vector2(16.8f, -10.8f), new Vector2(21.5f, -9.8f),
            };
            for (int i = 0; i < flowers.Length; i++)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Market_Flower_{i:D2}",
                    i % 2 == 0 ? flowerA : flowerCluster, flowers[i]);

            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Brook_Reed_01", reedTall, new Vector2(17.8f, -11.2f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Brook_Reed_02", reedTall, new Vector2(24.5f, -10.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Brook_Boulder_SE", boulder, new Vector2(25.2f, -9.6f));
        }

        static void PlaceTabletopProp(GameObject table, string name, Sprite sprite, Vector2 localOffset)
        {
            if (table == null || sprite == null) return;
            var go = new GameObject(name);
            go.transform.SetParent(table.transform, false);
            go.transform.localPosition = new Vector3(localOffset.x, localOffset.y, -0.001f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 0;
        }

        static void AddGatePillarCollider(GameObject parent, string name, float localX, float localY)
        {
            var pillar = new GameObject(name);
            pillar.transform.SetParent(parent.transform, false);
            pillar.transform.localPosition = new Vector3(localX, localY, 0f);
            var col = pillar.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.8f);
        }

        static void BuildTownBoundaryColliders(Scene scene)
        {
            var oldBounds = PixelCrawlerHubToolkit.FindRoot(scene, "MarketBounds");
            if (oldBounds != null) Object.DestroyImmediate(oldBounds);
            var root = new GameObject("MarketBounds");
            SceneManager.MoveGameObjectToScene(root, scene);

            AddBoundaryBox(root, "Bound_NorthBuildings", new Vector2(0f, 14.5f), new Vector2(62f, 3.0f));
            AddBoundaryBox(root, "Bound_West", new Vector2(-28.5f, -1.5f), new Vector2(1.4f, 34f));
            AddBoundaryBox(root, "Bound_East", new Vector2(28.0f, -1.5f), new Vector2(1.4f, 34f));
            AddBoundaryBox(root, "Bound_SouthW", new Vector2(-15.5f, -16.5f), new Vector2(26f, 1.4f));
            AddBoundaryBox(root, "Bound_SouthE", new Vector2(15.5f, -16.5f), new Vector2(26f, 1.4f));
            AddBoundaryBox(root, "Bound_SouthCap", new Vector2(0f, -21.0f), new Vector2(7f, 1.2f));
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

        // --- 2. Main Scene Actor & Population Bindings (Hub_LowerMarket.unity) -

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
                PixelCrawlerHubToolkit.DisableArtSceneLoader(scene);
                foreach (var oldName in new[] { "Environment", "EnvironmentGrid", "EnvironmentProps", "TownBoundaryColliders" })
                {
                    var oldGo = PixelCrawlerHubToolkit.FindRoot(scene, oldName);
                    if (oldGo != null) Object.DestroyImmediate(oldGo);
                }
                BuildMarketTerrain(scene);
                BuildNorthBuildings(scene);
                BuildMarketProps(scene);
                BuildTownBoundaryColliders(scene);

                var cam = FindInScene<CameraFollow2D>(scene);
                if (cam != null)
                {
                    cam.useBounds = true;
                    cam.boundsMin = new Vector2(-28.0f, -18.0f);
                    cam.boundsMax = new Vector2(28.0f, 19.0f);
                    var c = cam.GetComponent<Camera>();
                    if (c != null)
                    {
                        c.orthographicSize = 5.2f;
                        c.backgroundColor = new Color(0.09f, 0.14f, 0.09f, 1f);
                    }
                }

                var profiles = LoadNpcProfiles();

                var playerProfile = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Player", "Assets/Art/Characters/Player/Golem/Idle/Idle_Down-Sheet.png",
                    "Idle_Down-Sheet_0", "Assets/Animations/Player/Player_Golem.controller",
                    new Vector2(0f, -0.35f));
                BindRoot(scene, "Player", playerProfile);

                RepositionCoreAnchors(scene, profiles);
                BuildMarketPopulation(scene, profiles);

                // Bind static interactive props and hide doorway triggers drawn in Art overlay.
                BindStaticProp(scene, "NoticeBoard", "NoticeBoard", FantasyPropsDir + "BulletinBoard_1.png", "BulletinBoard_1");
                BindStaticProp(scene, "RestSpot", "RestSpot", ExtPropsPath, "Ext_Props_14");
                var restGo = PixelCrawlerHubToolkit.FindRoot(scene, "RestSpot");
                if (restGo != null)
                {
                    if (restGo.TryGetComponent<BoxCollider2D>(out var restCol))
                    {
                        restCol.isTrigger = true;
                        restCol.size = new Vector2(2.6f, 1.4f);
                    }
                    var solidTf = restGo.transform.Find("SolidBody");
                    var solidChild = solidTf != null ? solidTf.gameObject : null;
                    if (solidChild == null)
                    {
                        solidChild = new GameObject("SolidBody");
                        solidChild.transform.SetParent(restGo.transform, false);
                    }
                    solidChild.transform.localPosition = Vector3.zero;
                    var solidCol = solidChild.GetComponent<BoxCollider2D>();
                    if (solidCol == null)
                        solidCol = solidChild.AddComponent<BoxCollider2D>();
                    solidCol.isTrigger = false;
                    solidCol.size = new Vector2(2.2f, 0.65f);
                    solidCol.offset = new Vector2(0f, 0.35f);
                }
                var vaultGrate = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/Market_Vault_CellarGrate.png", 16f);
                if (vaultGrate != null)
                {
                    var vgGo = PixelCrawlerHubToolkit.FindRoot(scene, "VaultGate");
                    if (vgGo != null)
                    {
                        var vgProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                            "Market_VaultGrate", "Assets/Art/Environments/Custom/Market_Vault_CellarGrate.png",
                            vaultGrate.name, null, Vector2.zero);
                        PixelCrawlerHubToolkit.BindActorVisual(vgGo, vgProf, addYSort: true);
                    }
                }
                else
                {
                    BindStaticProp(scene, "VaultGate", "VaultGate", IntPropsPath, "Int_Interior_Props_01_45");
                }

                var lockpickDrain = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/Market_Drain_Lockpick.png", 16f);
                if (lockpickDrain != null)
                {
                    var lpGo = PixelCrawlerHubToolkit.FindRoot(scene, "BentLockpick");
                    if (lpGo != null)
                    {
                        var lpProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                            "Market_DrainLockpick", "Assets/Art/Environments/Custom/Market_Drain_Lockpick.png",
                            lockpickDrain.name, null, Vector2.zero);
                        PixelCrawlerHubToolkit.BindActorVisual(lpGo, lpProf, addYSort: true);
                    }
                }

                HideTriggerVisual(scene, "GuildEntrance");
                HideTriggerVisual(scene, "CaseDoor");
                var caseDoorGo = PixelCrawlerHubToolkit.FindRoot(scene, "CaseDoor");
                if (caseDoorGo != null && caseDoorGo.TryGetComponent<BoxCollider2D>(out var caseDoorCol))
                {
                    caseDoorCol.isTrigger = true;
                    caseDoorCol.size = new Vector2(2.0f, 1.8f);
                }

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

        static NpcProfiles LoadNpcProfiles() => new NpcProfiles
        {
            Eliza = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Eliza", "Assets/Art/Characters/NPCs/Named/Eliza/Idle/Idle_Down-Sheet.png",
                "Idle_Down-Sheet_0", "Assets/Animations/NPCs/NPC_Eliza.controller",
                new Vector2(0f, -0.35f)),
            PeasantF = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "PeasantF", "Assets/Art/Characters/NPCs/Citizen_F/Peasant_A/Idle/Idle-Sheet.png",
                "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_PeasantA.controller",
                new Vector2(0f, -0.35f), flipX: true),
            Ignatius = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Ignatius", "Assets/Art/Characters/NPCs/Named/Ignatius/Idle/Idle_Down-Sheet.png",
                "Idle_Down-Sheet_0", "Assets/Animations/NPCs/NPC_Ignatius.controller",
                new Vector2(0f, -0.35f)),
            Ovan = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Ovan", "Assets/Art/Characters/NPCs/Named/Ovan/Idle/Idle_Down-Sheet.png",
                "Idle_Down-Sheet_0", "Assets/Animations/NPCs/NPC_Ovan.controller",
                new Vector2(0f, -0.35f)),
            Carrier = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Carrier", "Assets/Art/Characters/NPCs/Named/Carrier/Idle/Idle_Down-Sheet.png",
                "Idle_Down-Sheet_0", "Assets/Animations/NPCs/NPC_Carrier.controller",
                new Vector2(0f, -0.35f)),
            GuardA = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Guard_A", "Assets/Art/Characters/NPCs/Citizen_M/Guard_A/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_GuardA.controller",
                new Vector2(0f, -0.35f), flipX: true),
            MerchantA = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Merchant_A", "Assets/Art/Characters/NPCs/Citizen_M/Merchant_A/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_MerchantA.controller",
                new Vector2(0f, -0.35f)),
            AdventurerA = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Adventurer_A", "Assets/Art/Characters/NPCs/Citizen_M/Adventurer_A/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_AdventurerA.controller",
                new Vector2(0f, -0.35f), flipX: true),
            VillagerA = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Villager_A", "Assets/Art/Characters/NPCs/Citizen_M/Villager_A/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_VillagerA.controller",
                new Vector2(0f, -0.35f)),
            MerchantB = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Merchant_B", "Assets/Art/Characters/NPCs/Citizen_M/Merchant_B/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_MerchantB.controller",
                new Vector2(0f, -0.35f)),
            SmithA = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Smith_A", "Assets/Art/Characters/NPCs/Citizen_M/Smith_A/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_SmithA.controller",
                new Vector2(0f, -0.35f)),
            TavernA = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Vendor", "Assets/Art/Characters/NPCs/Citizen_F/Tavern_A/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_TavernA.controller",
                new Vector2(0f, -0.35f)),
            TavernB = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Townsperson", "Assets/Art/Characters/NPCs/Citizen_F/Tavern_B/Idle/Idle_Side-Sheet.png",
                "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_TavernB.controller",
                new Vector2(0f, -0.35f), flipX: true),
        };

        static void RepositionCoreAnchors(Scene scene, NpcProfiles profiles)
        {
            // Doors, portals, & landmarks spread sensibly across all 4 market districts.
            EnsureGuildEntrance(scene);
            EnsureSpawnMarker(scene, "Spawn_GuildDoor", "guild_door", new Vector3(0.0f, 11.8f, 0f));
            SetPos(scene, "Player", new Vector3(0.0f, -2.5f, 0f));
            SetPos(scene, "PlayerSpawn", new Vector3(0.0f, -2.5f, 0f));
            SetPos(scene, "VaultGate", new Vector3(-9.5f, 12.2f, 0f));
            SetPos(scene, "BentLockpick", new Vector3(-9.5f, 10.0f, 0f));
            SetPos(scene, "CaseDoor", new Vector3(-20.5f, 2.5f, 0f));
            SetPos(scene, "Carrier", new Vector3(-23.2f, -1.7f, 0f));
            SetPos(scene, "NoticeBoard", new Vector3(-3.6f, 10.0f, 0f));
            var noticeGo = PixelCrawlerHubToolkit.FindRoot(scene, "NoticeBoard");
            if (noticeGo != null && noticeGo.TryGetComponent<InspectionInteractable>(out var notice))
            {
                notice.blocksMovement = true;
                if (notice.inspectionDialogue == null)
                    notice.inspectionDialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(
                        $"{FlavorDialogueFolder}/D_NoticeBoard_01.asset");
            }
            // RestSpot occupies the shaded wooden bench in Central Park's NW quadrant.
            var restGo = PixelCrawlerHubToolkit.FindRoot(scene, "RestSpot");
            if (restGo == null)
            {
                HubSceneFactory.CreateInteractable("RestSpot",
                    new Color(0.4f, 0.55f, 0.35f), new Vector3(-5.2f, 4.2f, 0f), out RestSpot rest);
                rest.promptText = "[E] Rest";
                SceneManager.MoveGameObjectToScene(rest.gameObject, scene);
            }
            else
            {
                restGo.transform.position = new Vector3(-5.2f, 4.2f, 0f);
            }

            // Ovan's office is upstairs inside Hub_Guild; keep the outdoor fallback inactive.
            var ovan = PixelCrawlerHubToolkit.FindRoot(scene, "Ovan");
            if (ovan != null)
            {
                PixelCrawlerHubToolkit.BindActorVisual(ovan, profiles.Ovan, addYSort: true);
                ovan.SetActive(false);
            }

            // Key & Existing Crowd NPCs cleanly separated into the 4 functional zones.
            SetPos(scene, "Ignatius", new Vector3(-15.0f, 11.4f, 0f));
            var barks = AssetDatabase.LoadAssetAtPath<DialogueData>(BarksPath);
            EnsureTalkNpc(scene, "Vendor", new Vector3(15.5f, 3.6f, 0f), barks);
            EnsureTalkNpc(scene, "Townsperson", new Vector3(-18.5f, 2.2f, 0f), barks);
            EnsureCrowdMember(scene, "MarketCrowd", new Vector3(-3.2f, 1.5f, 0f), barks);
            EnsureCrowdMember(scene, "MarketCrowd_Vendor", new Vector3(19.0f, 3.8f, 0f), barks);
            EnsureCrowdMember(scene, "MarketCrowd_Townsperson", new Vector3(2.8f, -2.5f, 0f), barks);
            EnsureCrowdMember(scene, "MarketCrowd_Adventurer", new Vector3(12.5f, 0.8f, 0f), barks);

            var arrivalEliza = EnsureArrivalEliza(scene, new Vector3(-24.0f, 0.0f, 0f));

            BindRoot(scene, "ArrivalEliza", profiles.Eliza);
            BindRoot(scene, "Ignatius", profiles.Ignatius);
            BindRoot(scene, "Vendor", profiles.TavernA);
            BindRoot(scene, "MarketCrowd_Vendor", profiles.TavernA);
            BindRoot(scene, "Townsperson", profiles.PeasantF);
            BindRoot(scene, "MarketCrowd", profiles.TavernB);
            BindRoot(scene, "MarketCrowd_Townsperson", profiles.PeasantF);
            BindRoot(scene, "MarketCrowd_Adventurer", profiles.AdventurerA);
            BindRoot(scene, "Carrier", profiles.Carrier);

            // Arrival sequence spanning the West Arrival Road -> Central Park -> Investigator's Guild Hall.
            var arrival = EnsureMarketArrivalController(scene, arrivalEliza.transform);
            arrival.playerEntry = new Vector3(-25.5f, 0.0f, 0f);
            arrival.elizaEntry = new Vector3(-24.0f, 0.0f, 0f);
            arrival.elizaLead = new Vector3(-11.0f, 0.0f, 0f);
            arrival.elizaDoor = new Vector3(0.0f, 12.1f, 0f);
            arrival.playerDoor = new Vector3(0.5f, 11.7f, 0f);
            arrival.elizaInside = new Vector3(0.0f, 13.0f, 0f);
        }

        static void EnsureGuildEntrance(Scene scene)
        {
            var pos = new Vector3(0.0f, 12.9f, 0f);
            var doorGo = PixelCrawlerHubToolkit.FindRoot(scene, "GuildEntrance");
            GateInteractable door;
            if (doorGo == null)
            {
                doorGo = HubSceneFactory.CreateInteractable("GuildEntrance",
                    new Color(0.55f, 0.55f, 0.65f), pos, out door);
                SceneManager.MoveGameObjectToScene(doorGo, scene);
            }
            else
            {
                doorGo.transform.position = pos;
                door = doorGo.GetComponent<GateInteractable>() ?? doorGo.AddComponent<GateInteractable>();
            }

            doorGo.SetActive(true);
            door.promptText = "[E] Enter";
            door.requiredFlag = "";
            door.targetScene = "Hub_Guild";

            var col = doorGo.GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.isTrigger = true;
                col.size = new Vector2(2.0f, 1.6f);
            }

            var nameTransform = doorGo.transform.Find("NameLabel");
            if (nameTransform != null && nameTransform.TryGetComponent<InteractableLabel>(out var label))
                label.text = "Investigator's Guild";
        }

        static void EnsureSpawnMarker(Scene scene, string rootName, string entryId, Vector3 pos)
        {
            var markerGo = PixelCrawlerHubToolkit.FindRoot(scene, rootName);
            if (markerGo == null)
            {
                markerGo = new GameObject(rootName);
                SceneManager.MoveGameObjectToScene(markerGo, scene);
            }
            markerGo.transform.position = pos;
            var marker = markerGo.GetComponent<SceneSpawnMarker>() ?? markerGo.AddComponent<SceneSpawnMarker>();
            marker.entryId = entryId;
        }

        static void EnsureTalkNpc(Scene scene, string name, Vector3 pos, DialogueData barks)
        {
            var go = PixelCrawlerHubToolkit.FindRoot(scene, name);
            if (go == null)
            {
                HubSceneFactory.CreateInteractable(name, new Color(0.6f, 0.5f, 0.4f), pos, out NpcInteractable npc);
                npc.promptText = "[E] Talk";
                npc.dialogue = barks;
                npc.blocksMovement = true;
                SceneManager.MoveGameObjectToScene(npc.gameObject, scene);
                return;
            }
            go.transform.position = pos;
            if (go.TryGetComponent<NpcInteractable>(out var existing))
            {
                existing.promptText = "[E] Talk";
                if (existing.dialogue == null) existing.dialogue = barks;
                existing.blocksMovement = true;
            }
        }

        static GameObject EnsureArrivalEliza(Scene scene, Vector3 pos)
        {
            var eliza = PixelCrawlerHubToolkit.FindRoot(scene, "ArrivalEliza");
            if (eliza == null)
            {
                eliza = HubSceneFactory.NewQuadObject("ArrivalEliza",
                    new Color(0.9f, 0.55f, 0.35f), pos);
                HubSceneFactory.ApplyVisualProfile(eliza);
                SceneManager.MoveGameObjectToScene(eliza, scene);
            }
            else
            {
                eliza.transform.position = pos;
            }
            if (eliza.GetComponent<YSort2D>() == null)
                eliza.AddComponent<YSort2D>();
            eliza.SetActive(false);
            return eliza;
        }

        static MarketArrivalController EnsureMarketArrivalController(Scene scene, Transform arrivalEliza)
        {
            var arrivalGo = PixelCrawlerHubToolkit.FindRoot(scene, "MarketArrivalController");
            if (arrivalGo == null)
            {
                arrivalGo = new GameObject("MarketArrivalController");
                SceneManager.MoveGameObjectToScene(arrivalGo, scene);
            }
            var arrival = arrivalGo.GetComponent<MarketArrivalController>()
                ?? arrivalGo.AddComponent<MarketArrivalController>();
            arrival.context = FindInScene<HubContext>(scene);
            var playerGo = PixelCrawlerHubToolkit.FindRoot(scene, "Player");
            arrival.player = playerGo != null ? playerGo.GetComponent<HubPlayerController>() : null;
            arrival.eliza = arrivalEliza;
            arrival.guildSceneName = "Hub_Guild";
            arrival.entryDialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(
                GuildIntroContentGenerator.Folder + "/D_Market_Arrival.asset");
            arrival.doorDialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(
                GuildIntroContentGenerator.Folder + "/D_Guild_Door.asset");
            return arrival;
        }

        static void BuildMarketPopulation(Scene scene, NpcProfiles profiles)
        {
            // Clean previous procedurally generated market extras for idempotent rebuilds.
            var toRemove = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name.StartsWith("MarketNpc_") || root.name.StartsWith("AmbientMarket_"))
                    toRemove.Add(root);
            }
            foreach (var go in toRemove) Object.DestroyImmediate(go);

            var defaultBarks = AssetDatabase.LoadAssetAtPath<DialogueData>(BarksPath);

            // --- Additional Interactable NPCs across the 4 zones ---
            SpawnInteractableNpc(scene, "MarketNpc_Smith",
                profiles.SmithA, new Vector3(-14.8f, -9.2f, 0f),
                EnsureFlavorDialogue("D_MarketSmith_01", "Forge Apprentice",
                    "Master Ignatius won't let anyone near that smelted lock. Funny thing is, the slag cooled too fast to be dragonfire.") ?? defaultBarks);

            SpawnInteractableNpc(scene, "MarketNpc_Apothecary",
                profiles.MerchantA, new Vector3(-20.2f, 11.4f, 0f),
                EnsureFlavorDialogue("D_MarketApoth_01", "Herbalist",
                    "Someone bought out half my lamp oil and sulfur-bane yesterday afternoon. Said they were 'cleaning an old cellar.'") ?? defaultBarks);

            SpawnInteractableNpc(scene, "MarketNpc_Chef",
                profiles.TavernB, new Vector3(19.8f, -2.8f, 0f),
                EnsureFlavorDialogue("D_MarketChef_01", "Grill Cook",
                    "Fresh honey-glazed skewers! Half the Watch ordered dinner early when the Guild sealed the vault lane.") ?? defaultBarks);

            SpawnInteractableNpc(scene, "MarketNpc_Guard",
                profiles.GuardA, new Vector3(-22.5f, 1.8f, 0f),
                EnsureFlavorDialogue("D_MarketGuard_01", "West Gate Watch",
                    "You came down from the ruined road? Good to see the eastern path finally open again—watch your step near the bazaar.") ?? defaultBarks);

            SpawnInteractableNpc(scene, "MarketNpc_Courier",
                profiles.VillagerA, new Vector3(2.4f, 10.3f, 0f),
                EnsureFlavorDialogue("D_MarketCourier_01", "Guild Courier",
                    "Dispatches have been piling up since sunrise. At least with the plaza widened, carts can finally turn around.") ?? defaultBarks);

            SpawnInteractableNpc(scene, "MarketNpc_Scholar",
                profiles.AdventurerA, new Vector3(4.5f, 9.6f, 0f),
                EnsureFlavorDialogue("D_MarketScholar_01", "Guild Archivist",
                    "The Investigator's Guild keeps duplicate vault keys under wax seal. If Ignatius lost his, the registry will say so.") ?? defaultBarks);

            SpawnInteractableNpc(scene, "MarketNpc_Baker",
                profiles.MerchantB, new Vector3(-17.8f, 6.2f, 0f),
                EnsureFlavorDialogue("D_MarketBaker_01", "Shopkeeper",
                    "Morning loaves are selling fast! Most of the crowd headed east to the open stalls once the well plaza cleared.") ?? defaultBarks);

            // --- Non-Interactable Strolling Market Citizens across avenues & park ---
            SpawnAmbientNpc(scene, "AmbientMarket_Stroll_North", profiles.MerchantB,
                new Vector3(-5.0f, 10.8f, 0f), new[]
                {
                    new Vector2(-5.0f, 10.8f), new Vector2(5.0f, 10.8f),
                    new Vector2(5.0f, 7.5f),   new Vector2(-5.0f, 7.5f)
                }, walkSpeed: 1.15f, pauseSeconds: 2.2f);

            SpawnAmbientNpc(scene, "AmbientMarket_Stroll_Plaza", profiles.PeasantF,
                new Vector3(-7.5f, 0.5f, 0f), new[]
                {
                    new Vector2(-7.5f, 0.5f), new Vector2(-7.5f, -7.5f),
                    new Vector2(7.5f, -7.5f), new Vector2(7.5f, 7.5f),
                    new Vector2(-7.5f, 7.5f)
                }, walkSpeed: 1.3f, pauseSeconds: 1.6f);

            SpawnAmbientNpc(scene, "AmbientMarket_Stroll_West", profiles.VillagerA,
                new Vector3(-23.0f, 3.0f, 0f), new[]
                {
                    new Vector2(-23.0f, 3.0f), new Vector2(-11.5f, 3.0f),
                    new Vector2(-11.5f, 9.2f), new Vector2(-23.0f, 9.2f)
                }, walkSpeed: 1.1f, pauseSeconds: 2.5f);

            SpawnAmbientNpc(scene, "AmbientMarket_Stroll_East", profiles.TavernA,
                new Vector3(11.5f, 0.8f, 0f), new[]
                {
                    new Vector2(11.5f, 0.8f),  new Vector2(22.5f, 0.8f),
                    new Vector2(22.5f, -1.2f), new Vector2(11.5f, -1.2f)
                }, walkSpeed: 1.2f, pauseSeconds: 2.0f);

            SpawnAmbientNpc(scene, "AmbientMarket_Stroll_South", profiles.PeasantF,
                new Vector3(-2.5f, -12.5f, 0f), new[]
                {
                    new Vector2(-2.5f, -12.5f), new Vector2(2.5f, -12.5f),
                    new Vector2(2.5f, -8.0f),   new Vector2(-2.5f, -8.0f)
                }, walkSpeed: 1.25f, pauseSeconds: 1.9f);

            SpawnAmbientNpc(scene, "AmbientMarket_WatchPatrol", profiles.GuardA,
                new Vector3(-19.5f, 0.0f, 0f), new[]
                {
                    new Vector2(-19.5f, 0.0f), new Vector2(-9.5f, 0.0f),
                    new Vector2(-9.5f, 3.2f),  new Vector2(-19.5f, 3.2f)
                }, walkSpeed: 1.35f, pauseSeconds: 1.4f);

            SpawnAmbientNpc(scene, "AmbientMarket_Stroll_MarketN", profiles.MerchantB,
                new Vector3(12.0f, 3.2f, 0f), new[]
                {
                    new Vector2(12.0f, 3.2f), new Vector2(20.5f, 3.2f)
                }, walkSpeed: 1.05f, pauseSeconds: 2.3f);

            SpawnAmbientNpc(scene, "AmbientMarket_Stroll_MarketS", profiles.TavernA,
                new Vector3(12.5f, -2.6f, 0f), new[]
                {
                    new Vector2(12.5f, -2.6f), new Vector2(21.0f, -2.6f)
                }, walkSpeed: 1.1f, pauseSeconds: 2.0f);

            // --- Non-Interactable Stationary Background Folk ---
            SpawnAmbientNpc(scene, "AmbientMarket_Idle_Lodge", profiles.MerchantB,
                new Vector2(-17.8f, 4.2f), null);
            SpawnAmbientNpc(scene, "AmbientMarket_Idle_Sawmill", profiles.SmithA,
                new Vector2(-8.2f, -11.5f), null);
            SpawnAmbientNpc(scene, "AmbientMarket_Idle_SouthGate", profiles.GuardA,
                new Vector2(2.2f, -13.2f), null);
            SpawnAmbientNpc(scene, "AmbientMarket_Idle_Brook", profiles.TavernB,
                new Vector2(16.5f, -8.8f), null);
            SpawnAmbientNpc(scene, "AmbientMarket_Idle_ParkNE", profiles.VillagerA,
                new Vector2(4.2f, 4.2f), null);
            SpawnAmbientNpc(scene, "AmbientMarket_Idle_ParkSW", profiles.PeasantF,
                new Vector2(-4.2f, -4.8f), null);
            SpawnAmbientNpc(scene, "AmbientMarket_Idle_ShopAlley", profiles.MerchantA,
                new Vector2(-24.2f, 9.2f), null);
        }

        static void SpawnInteractableNpc(
            Scene scene, string name, VisualProfile profile,
            Vector3 pos, DialogueData dialogue)
        {
            HubSceneFactory.CreateInteractable(name, Color.white, pos, out NpcInteractable npc);
            npc.promptText = "[E] Talk";
            npc.dialogue = dialogue;
            npc.blocksMovement = true;
            PixelCrawlerHubToolkit.BindActorVisual(npc.gameObject, profile, addYSort: true);
            SceneManager.MoveGameObjectToScene(npc.gameObject, scene);
        }

        static void SpawnAmbientNpc(
            Scene scene, string name, VisualProfile profile, Vector2 pos, Vector2[] waypoints,
            float walkSpeed = 1.2f, float pauseSeconds = 2.0f)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(pos.x, pos.y, pos.y * YSort2D.DepthPerUnitY);
            PixelCrawlerHubToolkit.BindActorVisual(go, profile, addYSort: true);

            if (waypoints != null && waypoints.Length > 1)
            {
                var stroller = go.AddComponent<AmbientStroller2D>();
                stroller.waypoints = waypoints;
                stroller.walkSpeed = walkSpeed;
                stroller.pauseSeconds = pauseSeconds;
                stroller.loop = true;
            }
        }

        static DialogueData EnsureFlavorDialogue(string assetId, string speaker, string line)
        {
            string path = $"{FlavorDialogueFolder}/{assetId}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (existing != null) return existing;

            PixelCrawlerHubToolkit.EnsureFolder(FlavorDialogueFolder);
            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.id = assetId;
            data.startNodeId = "line_1";
            data.nodes = new[]
            {
                new DialogueNodeData
                {
                    id = "line_1",
                    speakerName = speaker,
                    text = line,
                }
            };
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        static void EnsureCrowdMember(Scene scene, string name, Vector3 pos, DialogueData barks)
        {
            var go = PixelCrawlerHubToolkit.FindRoot(scene, name);
            if (go == null)
            {
                HubSceneFactory.CreateInteractable(name,
                    new Color(0.55f, 0.5f, 0.4f), pos, out NpcInteractable npc);
                npc.promptText = "[E] Listen";
                npc.dialogue = barks;
                npc.blocksMovement = true;
                SceneManager.MoveGameObjectToScene(npc.gameObject, scene);
            }
            else
            {
                go.transform.position = pos;
                if (go.TryGetComponent<NpcInteractable>(out var npc))
                {
                    npc.promptText = "[E] Listen";
                    if (npc.dialogue == null) npc.dialogue = barks;
                    npc.blocksMovement = true;
                }
            }
        }

        static void SetPos(Scene scene, string rootName, Vector3 pos)
        {
            var go = PixelCrawlerHubToolkit.FindRoot(scene, rootName);
            if (go != null) go.transform.position = pos;
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
            var ev = go.GetComponent<EntityVisual>();
            if (ev != null)
            {
                ev.Apply(null);
                Object.DestroyImmediate(ev);
            }
            var mr = go.GetComponent<MeshRenderer>();
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
