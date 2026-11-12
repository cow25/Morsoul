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
            RebuildArtOverlay();
            RebuildMainSceneBindings();
            AssetDatabase.SaveAssets();
            Debug.Log("LowerMarketSceneBuilder: rebuilt 3x expanded Hub_LowerMarket and Hub_LowerMarket_Art.");
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

            // Wide grass carpet covering full ultrawide camera overshoot bounds.
            PixelCrawlerHubToolkit.FillRect(grassMap, grassTile, -30, -18, 30, 18);

            // Dirt avenues: West Caravan Road, South Gate Boulevard, East Service Road, Market perimeter strips.
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -26, -2, -13, 1);
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, 14, -2, 26, 1);
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -2, -16, 2, -4);
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -18, -7, -8, -5);
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, 8, -7, 18, -5);
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -9, -7, 9, -4);

            // Grand Cobblestone Central Town Square, West/East Paved Market Stalls & North Guild Promenade.
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -9, -4, 9, 4);
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -18, 3, 17, 5);
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -17, -6, -8, 3);
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, 8, -6, 18, 4);

            // Town Brook wrapping the southeastern quarter safely off the paved bazaar.
            PixelCrawlerHubToolkit.FillRect(waterMap, waterTile, 10, -13, 24, -9);
            PixelCrawlerHubToolkit.FillRect(waterMap, waterTile, 18, -9, 24, -4);

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

            // District buildings across North Promenade (y = 5..8):
            // 1. West Awning Market Storefront (Fantasy Tileset House_Hay_4_Purple with shop awning; door aligns with CaseDoor at -14.5)
            var marketShopBuilding = LoadPackSprite(FantasyBuildingsDir + "House_Hay_4_Purple.png");
            if (marketShopBuilding != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Bazaar_Storefront_W",
                    marketShopBuilding, new Vector2(-16.5f, 4.95f), zBias: 0.25f, sortingOrder: 0,
                    colliderSize: new Vector2(7.6f, 1.4f), colliderOffset: new Vector2(0f, 0.7f));
            }
            else
            {
                PaintWallFacade(wallsMap, xStart: -18, width: 7, yBottom: 5, kitColOffset: 0);
            }

            // 2. Apothecary & Bakery (Kit 3: Tudor cream half-timber)
            PaintWallFacade(wallsMap, xStart: -9, width: 7, yBottom: 5, kitColOffset: 18);
            // 3. Grand Alchemist's Guild Hall (Kit 5: brick-base stucco)
            PaintWallFacade(wallsMap, xStart: 0, width: 7, yBottom: 5, kitColOffset: 30);
            // 4. Dwarven Vault Workshop (Kit 4: cream stucco)
            PaintWallFacade(wallsMap, xStart: 9, width: 7, yBottom: 5, kitColOffset: 24);

            wallsMap.RefreshAllTiles();

            var roofBrown = PixelCrawlerHubToolkit.LoadSprite(RoofsPath, "Ext_Roofs_00");
            var roofGreen = PixelCrawlerHubToolkit.LoadSprite(RoofsPath, "Ext_Roofs_01");
            var roofSlate = PixelCrawlerHubToolkit.LoadSprite(RoofsPath, "Ext_Roofs_04");

            if (marketShopBuilding == null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Lodge_Roof",
                    roofBrown, new Vector2(-14.5f, 7.0f), zBias: -0.4f, sortingOrder: -1);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Apoth_Roof",
                roofBrown, new Vector2(-5.5f, 7.0f), zBias: -0.4f, sortingOrder: -1);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Roof",
                roofGreen ?? roofBrown, new Vector2(3.0f, 7.2f), zBias: -0.45f, sortingOrder: -1);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Vault_Roof",
                roofSlate ?? roofBrown, new Vector2(12.5f, 7.25f), zBias: -0.42f, sortingOrder: -1);

            var cottageDoor = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_06");
            var guildArchDoor = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_00");
            var windowShuttered = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_08");
            var windowTall = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_11");
            var flowerBox = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_12");
            var redBanner = PixelCrawlerHubToolkit.LoadSprite(OtherObjectsSheetPath, "Other_objects_15");
            var blueBanner = PixelCrawlerHubToolkit.LoadSprite(OtherObjectsSheetPath, "Other_objects_16");
            var greenBanner = PixelCrawlerHubToolkit.LoadSprite(DungeonPropsPath, "Dungeon_Props_16");
            var signRight = LoadPackSprite(FantasyPropsDir + "Sign_1.png");

            // 1. West Market Storefront details (baskets cleanly flanking the west storefront with Y-sort and solid colliders).
            var basketCitrus = LoadPackSprite(VillagePropsDir + "kitchen_props4_2.png");
            var basketApples = LoadPackSprite(VillagePropsDir + "kitchen_props4_3.png");
            if (basketCitrus != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Storefront_Basket_A", basketCitrus,
                    new Vector2(-18.2f, 4.55f), colliderSize: new Vector2(0.85f, 0.55f), colliderOffset: new Vector2(0f, 0.25f));
            if (basketApples != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Storefront_Basket_B", basketApples,
                    new Vector2(-17.3f, 4.55f), colliderSize: new Vector2(0.85f, 0.55f), colliderOffset: new Vector2(0f, 0.25f));
            if (signRight != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Storefront_Sign", signRight, new Vector2(-12.1f, 4.7f));

            // 2. Apothecary & Bakery details.
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Apoth_Door", cottageDoor, new Vector2(-5.5f, 5.05f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Apoth_Win_W", windowShuttered, new Vector2(-7.3f, 5.65f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Apoth_Win_E", windowShuttered, new Vector2(-3.7f, 5.65f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Apoth_Flower_W", flowerBox, new Vector2(-7.3f, 5.0f), zBias: 0.24f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Apoth_Flower_E", flowerBox, new Vector2(-3.7f, 5.0f), zBias: 0.24f);
            if (blueBanner != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Apoth_Banner", blueBanner, new Vector2(-4.4f, 5.45f), zBias: 0.25f);

            // 3. Guild Hall facade — arched door centered at x=3.0 behind GuildEntrance.
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Door", guildArchDoor, new Vector2(3.0f, 5.0f), zBias: 0.25f);
            if (signRight != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Sign", signRight, new Vector2(1.9f, 4.45f));
            if (greenBanner != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Banner_W", greenBanner, new Vector2(2.0f, 5.45f), zBias: 0.25f);
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Banner_E", greenBanner, new Vector2(4.0f, 5.45f), zBias: 0.25f);
            }
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Win_W", windowTall ?? windowShuttered, new Vector2(0.7f, 5.55f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Win_E", windowTall ?? windowShuttered, new Vector2(5.3f, 5.55f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Flower_W", flowerBox, new Vector2(0.7f, 5.0f), zBias: 0.24f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Guild_Flower_E", flowerBox, new Vector2(5.3f, 5.0f), zBias: 0.24f);

            // 4. Dwarven Vault Workshop details (VaultGate interactive prop provides the doorway at 13.5, 5.0).
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Vault_Win_W", windowTall ?? windowShuttered, new Vector2(10.8f, 5.65f), zBias: 0.25f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Vault_Win_E", windowShuttered, new Vector2(15.2f, 5.65f), zBias: 0.25f);
            if (redBanner != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(propsRoot, "Vault_Banner", redBanner, new Vector2(11.9f, 5.45f), zBias: 0.25f);
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

            // --- 1. West Produce & Spice Bazaar (Vendor at -8.0, -4.55) ---
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_West_Counter", stallStandLeft,
                new Vector2(-11.2f, -5.15f), colliderSize: new Vector2(3.6f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Rack_W0", rackProduceA,
                new Vector2(-14.2f, -3.55f), colliderSize: new Vector2(2.0f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Rack_W1", rackProduceB,
                new Vector2(-11.8f, -3.55f), colliderSize: new Vector2(2.0f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Rack_W2", rackProduceC,
                new Vector2(-9.5f, -3.55f), colliderSize: new Vector2(2.0f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));

            // Overflowing produce baskets & grain depot around West Stall.
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Basket_Citrus", fruitBasket, new Vector2(-13.4f, -5.25f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Basket_Apples", appleBasket, new Vector2(-12.4f, -5.3f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Basket_Red", redAppleBasket, new Vector2(-9.2f, -5.25f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Basket_Potato", potatoBasket, new Vector2(-14.3f, -5.25f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Watermelon", watermelon, new Vector2(-10.2f, -5.05f));

            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Caravan_Crates_W", flourCrates,
                new Vector2(-16.9f, -3.75f), colliderSize: new Vector2(2.8f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Caravan_Barrels_W", barrelGroup,
                new Vector2(-17.4f, -5.3f), colliderSize: new Vector2(1.8f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Caravan_Sacks_W", sackWide, new Vector2(-15.5f, -5.4f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Caravan_GrainSack_W", grainSackStack ?? sackWide, new Vector2(-15.4f, -4.15f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Banner_W", purpleBanner, new Vector2(-18.8f, -3.2f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Bazaar_Sign_W", signRight, new Vector2(-7.0f, -3.3f));

            // --- 2. North Apothecary & Herb Shop (uncluttered facade, book + candle on table) ---
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Apoth_Shelf_A", apothShelfA,
                new Vector2(-9.25f, 4.6f), colliderSize: new Vector2(1.8f, 0.85f), colliderOffset: new Vector2(0f, 0.42f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Apoth_HerbCrate", herbCrate,
                new Vector2(-9.5f, 3.35f), colliderSize: new Vector2(0.9f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
            var apothTable = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Apoth_Table", marketTable,
                new Vector2(-7.8f, 2.95f), colliderSize: new Vector2(2.0f, 0.95f), colliderOffset: new Vector2(0f, 0.45f));
            PlaceTabletopProp(apothTable, "Apoth_Book", openBook, new Vector2(-0.35f, 0.65f));
            PlaceTabletopProp(apothTable, "Apoth_Candle", litCandle, new Vector2(0.35f, 0.68f));

            // --- 3. Central Guild Promenade Seating & Banners (RestSpot bench sits beside NoticeBoard at -2.45, 2.3) ---
            var cafeTable = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Plaza_Cafe_Table", marketTable,
                new Vector2(5.4f, -0.6f), colliderSize: new Vector2(2.0f, 0.95f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Plaza_Cafe_Bench_W", smallBench, new Vector2(4.0f, -0.55f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Plaza_Cafe_Bench_E", smallBench, new Vector2(6.8f, -0.55f));
            PlaceTabletopProp(cafeTable, "Plaza_Cafe_Pie", bakedPie, new Vector2(0f, 0.72f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Guild_StandBanner_W", purpleBanner, new Vector2(1.4f, 4.15f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Guild_StandBanner_E", purpleBanner, new Vector2(4.6f, 4.15f));

            // --- 4. East Artisan Blacksmith & Armory Yard (Ignatius 8.5,2.8 / Smith 12.2,3.2) ---
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Forge_Blast", stoneFurnace,
                new Vector2(10.0f, 4.65f), colliderSize: new Vector2(1.8f, 1.2f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Forge_CoalPile", coalPile, new Vector2(11.7f, 4.4f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Forge_Smelter", brickSmelter,
                new Vector2(16.8f, 4.55f), colliderSize: new Vector2(2.2f, 1.3f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Armory_Rack_A", weaponRackA,
                new Vector2(10.6f, 1.9f), colliderSize: new Vector2(2.1f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Armory_Rack_B", weaponRackB,
                new Vector2(15.2f, 2.15f), colliderSize: new Vector2(2.1f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Armory_Armor_W", armorStandA, new Vector2(8.5f, 4.55f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Armory_Armor_E", armorStandB, new Vector2(15.2f, 4.55f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Forge_QuenchBarrel", singleBarrel, new Vector2(17.9f, 2.35f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Forge_CiderCask", ciderBarrel, new Vector2(18.0f, 3.45f));
            if (blueCrystal != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Vault_CrystalAccent", blueCrystal, new Vector2(15.5f, 5.15f));

            // --- 5. East Tavern Roast & Provisioner Stall (MarketCrowd_Vendor 12.2,-4.65 / Chef 14.2,-2.45) ---
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_Counter", stallStandRight,
                new Vector2(14.3f, -5.15f), colliderSize: new Vector2(2.1f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_RoastRack", roastRack,
                new Vector2(16.7f, -4.95f), colliderSize: new Vector2(2.5f, 0.95f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_Barrels", barrelGroup,
                new Vector2(10.8f, -4.85f), colliderSize: new Vector2(1.7f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_AleKeg", aleKeg,
                new Vector2(9.2f, -4.85f), colliderSize: new Vector2(1.4f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_BreadBasket", breadBasket, new Vector2(13.0f, -5.3f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_FruitBasket", fruitBasket, new Vector2(15.5f, -5.3f));
            var serviceTable = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_ServiceTable", marketTable,
                new Vector2(16.0f, -3.1f), colliderSize: new Vector2(2.0f, 0.95f), colliderOffset: new Vector2(0f, 0.45f));
            PlaceTabletopProp(serviceTable, "Stall_East_Dish", foodPlate, new Vector2(-0.35f, 0.72f));
            PlaceTabletopProp(serviceTable, "Stall_East_StewPot", stewPot, new Vector2(0.4f, 0.75f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Stall_East_Sign", signLeft, new Vector2(8.6f, -3.2f));

            // --- 6. South Square Kiosks, Gate Archway & Supply Depot ---
            if (cityGateArch != null)
            {
                var gate = PixelCrawlerHubToolkit.PlaceSpriteProp(root, "South_CityGate_Arch", cityGateArch,
                    new Vector2(0.0f, -11.0f), zBias: -0.25f, sortingOrder: 5);
                if (gate != null)
                {
                    AddGatePillarCollider(gate, "ArchPillar_W", -2.05f, 2.5f);
                    AddGatePillarCollider(gate, "ArchPillar_E", 2.05f, 2.5f);
                }
            }
            if (sawmill != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "South_Sawmill", sawmill,
                    new Vector2(-7.3f, -6.1f), colliderSize: new Vector2(1.6f, 1.1f), colliderOffset: new Vector2(0f, 0.4f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "South_TimberPile", timberLog, new Vector2(-5.4f, -6.25f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "South_PotatoBasket", potatoBasket, new Vector2(-9.5f, -6.6f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "South_GrainCrates", grainCrates,
                new Vector2(4.7f, -6.0f), colliderSize: new Vector2(1.9f, 0.95f), colliderOffset: new Vector2(0f, 0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "South_Sack", sackWide, new Vector2(6.5f, -6.1f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "South_GateBanner_W", bannerRed, new Vector2(-2.65f, -6.3f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "South_GateBanner_E", bannerBlue, new Vector2(2.65f, -6.3f));

            // --- 7. Warm Double-Lantern Street Lamps (roadside corners only, clear of storefronts & tables) ---
            var lampPositions = new[]
            {
                new Vector2(-19.3f, 1.6f), new Vector2(17.6f, 2.1f),
                new Vector2(-4.6f, -7.2f), new Vector2(4.6f, -7.2f),
            };
            for (int i = 0; i < lampPositions.Length; i++)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Market_Lamp_{i:D2}", lampDouble,
                    lampPositions[i], colliderSize: new Vector2(0.6f, 0.4f), colliderOffset: new Vector2(0f, 0.2f));
            }

            // --- Perimeter Forest Framing (keep South Gate road and SE TownBrook clear) ---
            Sprite[] borderTrees = { oakGreen, pineA, oakGold, pineB };
            var treeCoords = new List<Vector2>
            {
                // West border trees
                new Vector2(-21.2f, 6.2f), new Vector2(-22.5f, 3.5f), new Vector2(-21.8f, -2.8f), new Vector2(-21.0f, -6.5f),
                // North deep forest crown behind buildings
                new Vector2(-20.0f, 8.2f), new Vector2(-10.5f, 7.6f), new Vector2(-0.8f, 8.2f),
                new Vector2(8.2f, 7.8f),   new Vector2(18.0f, 8.0f),  new Vector2(21.0f, 5.5f),
                // East border trees (north of brook)
                new Vector2(20.8f, 2.6f),  new Vector2(21.4f, -0.8f),
            };
            for (int x = -22; x <= 9; x += 3)
            {
                if (x >= -2 && x <= 2) continue; // Keep South Gate road clear
                if (x >= -8 && x <= -6) continue; // Keep South Sawmill clear of canopy overlap
                treeCoords.Add(new Vector2(x, -11.4f - (Mathf.Abs(x) % 2) * 0.5f));
            }
            for (int i = 0; i < treeCoords.Count; i++)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Market_Tree_{i:D2}",
                    borderTrees[i % borderTrees.Length], treeCoords[i]);

            // --- Bushes, Flowers, Reeds & Boulders ---
            var bushes = new[]
            {
                new Vector2(-19.0f, 3.8f), new Vector2(-9.8f, 4.4f), new Vector2(7.8f, 4.6f),
                new Vector2(18.2f, 4.0f),  new Vector2(-18.6f, -5.8f), new Vector2(-10.6f, -7.2f),
                new Vector2(8.2f, -6.8f),  new Vector2(-4.2f, -8.2f), new Vector2(4.2f, -8.2f),
            };
            for (int i = 0; i < bushes.Length; i++)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Market_Bush_{i:D2}",
                    i % 3 == 0 ? bushSmall : bushMed, bushes[i]);

            var flowers = new[]
            {
                new Vector2(-17.4f, 4.4f), new Vector2(-10.5f, 3.9f), new Vector2(-2.9f, 4.25f),
                new Vector2(6.8f, 4.1f),   new Vector2(-12.2f, -6.3f), new Vector2(9.2f, -6.1f),
                new Vector2(11.8f, -7.2f), new Vector2(15.5f, -7.3f),
            };
            for (int i = 0; i < flowers.Length; i++)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Market_Flower_{i:D2}",
                    i % 2 == 0 ? flowerA : flowerCluster, flowers[i]);

            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Brook_Reed_01", reedTall, new Vector2(11.4f, -8.2f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Brook_Reed_02", reedTall, new Vector2(16.2f, -8.3f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Brook_Boulder_SE", boulder, new Vector2(17.8f, -7.8f));
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

            AddBoundaryBox(root, "Bound_NorthBuildings", new Vector2(0f, 6.0f), new Vector2(44f, 2.1f));
            AddBoundaryBox(root, "Bound_West", new Vector2(-20.2f, -1.5f), new Vector2(1.4f, 17f));
            AddBoundaryBox(root, "Bound_East", new Vector2(19.2f, -1.5f), new Vector2(1.4f, 17f));
            AddBoundaryBox(root, "Bound_SouthW", new Vector2(-11.2f, -8.5f), new Vector2(18.5f, 1.4f));
            AddBoundaryBox(root, "Bound_SouthE", new Vector2(11.2f, -8.5f), new Vector2(18.5f, 1.4f));
            AddBoundaryBox(root, "Bound_SouthCap", new Vector2(0f, -11.0f), new Vector2(6f, 1.2f));
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

                var cam = FindInScene<CameraFollow2D>(scene);
                if (cam != null)
                {
                    cam.useBounds = true;
                    cam.boundsMin = new Vector2(-19.5f, -7.5f);
                    cam.boundsMax = new Vector2(19.5f, 7.0f);
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
                BindStaticProp(scene, "VaultGate", "VaultGate", IntPropsPath, "Int_Interior_Props_01_45");

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
            EnsureSpawnMarker(scene, "Spawn_GuildDoor", "guild_door", new Vector3(3.0f, 4.1f, 0f));
            SetPos(scene, "VaultGate", new Vector3(13.5f, 5.0f, 0f));
            SetPos(scene, "CaseDoor", new Vector3(-14.5f, 4.9f, 0f));
            SetPos(scene, "Carrier", new Vector3(-16.0f, -0.8f, 0f));
            SetPos(scene, "NoticeBoard", new Vector3(1.05f, 2.2f, 0f));
            var noticeGo = PixelCrawlerHubToolkit.FindRoot(scene, "NoticeBoard");
            if (noticeGo != null && noticeGo.TryGetComponent<InspectionInteractable>(out var notice))
            {
                notice.blocksMovement = true;
                if (notice.inspectionDialogue == null)
                    notice.inspectionDialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(
                        $"{FlavorDialogueFolder}/D_NoticeBoard_01.asset");
            }
            // RestSpot occupies the wooden bench right beside the NoticeBoard.
            var restGo = PixelCrawlerHubToolkit.FindRoot(scene, "RestSpot");
            if (restGo == null)
            {
                HubSceneFactory.CreateInteractable("RestSpot",
                    new Color(0.4f, 0.55f, 0.35f), new Vector3(-2.45f, 2.3f, 0f), out RestSpot rest);
                rest.promptText = "[E] Rest";
                SceneManager.MoveGameObjectToScene(rest.gameObject, scene);
            }
            else
            {
                restGo.transform.position = new Vector3(-2.45f, 2.3f, 0f);
            }

            // Ovan's office is upstairs inside Hub_Guild; keep the outdoor fallback inactive.
            var ovan = PixelCrawlerHubToolkit.FindRoot(scene, "Ovan");
            if (ovan != null)
            {
                PixelCrawlerHubToolkit.BindActorVisual(ovan, profiles.Ovan, addYSort: true);
                ovan.SetActive(false);
            }

            // Key & Existing Crowd NPCs cleanly separated into market zones.
            SetPos(scene, "Ignatius", new Vector3(8.5f, 2.8f, 0f));
            var barks = AssetDatabase.LoadAssetAtPath<DialogueData>(BarksPath);
            EnsureTalkNpc(scene, "Vendor", new Vector3(-8.0f, -4.55f, 0f), barks);
            EnsureTalkNpc(scene, "Townsperson", new Vector3(-13.4f, 3.6f, 0f), barks);
            EnsureCrowdMember(scene, "MarketCrowd", new Vector3(-5.2f, 1.4f, 0f), barks);
            EnsureCrowdMember(scene, "MarketCrowd_Vendor", new Vector3(12.4f, -3.75f, 0f), barks);
            EnsureCrowdMember(scene, "MarketCrowd_Townsperson", new Vector3(1.4f, -3.1f, 0f), barks);
            EnsureCrowdMember(scene, "MarketCrowd_Adventurer", new Vector3(9.5f, -0.4f, 0f), barks);

            var arrivalEliza = EnsureArrivalEliza(scene, new Vector3(-15.0f, -0.5f, 0f));

            BindRoot(scene, "ArrivalEliza", profiles.Eliza);
            BindRoot(scene, "Ignatius", profiles.Ignatius);
            BindRoot(scene, "Vendor", profiles.TavernA);
            BindRoot(scene, "MarketCrowd_Vendor", profiles.TavernA);
            BindRoot(scene, "Townsperson", profiles.PeasantF);
            BindRoot(scene, "MarketCrowd", profiles.TavernB);
            BindRoot(scene, "MarketCrowd_Townsperson", profiles.PeasantF);
            BindRoot(scene, "MarketCrowd_Adventurer", profiles.AdventurerA);
            BindRoot(scene, "Carrier", profiles.Carrier);

            // Arrival sequence spanning the expanded West Boulevard -> Guild Hall.
            var arrival = EnsureMarketArrivalController(scene, arrivalEliza.transform);
            arrival.playerEntry = new Vector3(-16.5f, -0.5f, 0f);
            arrival.elizaEntry = new Vector3(-15.0f, -0.5f, 0f);
            arrival.elizaLead = new Vector3(-9.5f, -0.5f, 0f);
            arrival.elizaDoor = new Vector3(3.0f, 4.2f, 0f);
            arrival.playerDoor = new Vector3(3.5f, 3.9f, 0f);
            arrival.elizaInside = new Vector3(3.0f, 5.1f, 0f);
        }

        static void EnsureGuildEntrance(Scene scene)
        {
            var pos = new Vector3(3.0f, 4.9f, 0f);
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
                label.text = "Merchant Guild";
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

            // --- Additional Random Interactable NPCs across districts ---
            SpawnInteractableNpc(scene, "MarketNpc_Smith",
                profiles.SmithA, new Vector3(12.2f, 3.2f, 0f),
                EnsureFlavorDialogue("D_MarketSmith_01", "Forge Apprentice",
                    "Master Ignatius won't let anyone near that smelted lock. Funny thing is, the slag cooled too fast to be dragonfire.") ?? defaultBarks);

            SpawnInteractableNpc(scene, "MarketNpc_Apothecary",
                profiles.MerchantA, new Vector3(-5.9f, 3.25f, 0f),
                EnsureFlavorDialogue("D_MarketApoth_01", "Herbalist",
                    "Someone bought out half my lamp oil and sulfur-bane yesterday afternoon. Said they were 'cleaning an old cellar.'") ?? defaultBarks);

            SpawnInteractableNpc(scene, "MarketNpc_Chef",
                profiles.TavernB, new Vector3(14.2f, -2.45f, 0f),
                EnsureFlavorDialogue("D_MarketChef_01", "Grill Cook",
                    "Fresh honey-glazed skewers! Half the Watch ordered dinner early when the Guild sealed the vault lane.") ?? defaultBarks);

            SpawnInteractableNpc(scene, "MarketNpc_Guard",
                profiles.GuardA, new Vector3(-16.2f, 1.4f, 0f),
                EnsureFlavorDialogue("D_MarketGuard_01", "West Gate Watch",
                    "You came down from the ruined road? Good to see the eastern path finally open again—watch your step near the bazaar.") ?? defaultBarks);

            SpawnInteractableNpc(scene, "MarketNpc_Courier",
                profiles.VillagerA, new Vector3(-4.6f, -2.5f, 0f),
                EnsureFlavorDialogue("D_MarketCourier_01", "Guild Courier",
                    "Dispatches have been piling up since sunrise. At least with the plaza widened, carts can finally turn around.") ?? defaultBarks);

            // --- Non-Interactable Strolling Market Citizens (cohesive Pixel Crawler sprites) ---
            SpawnAmbientNpc(scene, "AmbientMarket_Stroll_North", profiles.MerchantB,
                new Vector3(-4.2f, 3.5f, 0f), new[]
                {
                    new Vector2(-4.2f, 3.5f), new Vector2(5.5f, 3.5f),
                    new Vector2(-4.2f, 3.5f), new Vector2(-11.2f, 3.5f)
                }, walkSpeed: 1.15f, pauseSeconds: 2.2f);

            SpawnAmbientNpc(scene, "AmbientMarket_Stroll_Plaza", profiles.PeasantF,
                new Vector2(-6.5f, 0.4f), new[]
                {
                    new Vector2(-6.5f, 0.4f),  new Vector2(-6.5f, -2.6f),
                    new Vector2(2.6f, -2.6f),  new Vector2(2.6f, 1.2f)
                }, walkSpeed: 1.3f, pauseSeconds: 1.6f);

            SpawnAmbientNpc(scene, "AmbientMarket_Stroll_West", profiles.VillagerA,
                new Vector2(-13.8f, -1.8f), new[]
                {
                    new Vector2(-13.8f, -1.8f), new Vector2(-7.8f, -1.8f)
                }, walkSpeed: 1.1f, pauseSeconds: 2.5f);

            SpawnAmbientNpc(scene, "AmbientMarket_Stroll_East", profiles.TavernA,
                new Vector2(11.4f, 1.4f), new[]
                {
                    new Vector2(11.4f, 1.4f),  new Vector2(16.0f, 1.2f),
                    new Vector2(16.0f, -1.5f), new Vector2(11.4f, -1.5f)
                }, walkSpeed: 1.2f, pauseSeconds: 2.0f);

            SpawnAmbientNpc(scene, "AmbientMarket_Stroll_South", profiles.PeasantF,
                new Vector2(-3.2f, -4.3f), new[]
                {
                    new Vector2(-3.2f, -4.3f), new Vector2(6.8f, -4.3f)
                }, walkSpeed: 1.25f, pauseSeconds: 1.9f);

            SpawnAmbientNpc(scene, "AmbientMarket_WatchPatrol", profiles.GuardA,
                new Vector2(-10.4f, 0.5f), new[]
                {
                    new Vector2(-10.4f, 0.5f), new Vector2(-5.5f, 0.5f),
                    new Vector2(-5.5f, 2.5f),  new Vector2(-10.4f, 2.5f)
                }, walkSpeed: 1.35f, pauseSeconds: 1.4f);

            // --- Non-Interactable Stationary Background Folk (mixed townsfolk idling cleanly off props/flowers) ---
            SpawnAmbientNpc(scene, "AmbientMarket_Idle_Lodge", profiles.MerchantB,
                new Vector2(-15.4f, 3.4f), null);
            SpawnAmbientNpc(scene, "AmbientMarket_Idle_Sawmill", profiles.SmithA,
                new Vector2(-6.2f, -5.65f), null);
            SpawnAmbientNpc(scene, "AmbientMarket_Idle_SouthGate", profiles.GuardA,
                new Vector2(1.45f, -4.55f), null);
            SpawnAmbientNpc(scene, "AmbientMarket_Idle_Brook", profiles.TavernB,
                new Vector2(13.8f, -6.5f), null);
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
