using Morsoul.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Rebuilds Hub_IronHollow and its art overlay Hub_IronHollow_Art using
    /// Pixel Crawler slate cavern flagstones, dark Wall_Variations rock cliffs
    /// with timber mine portals, timber forge decks, glowing blast furnaces,
    /// coal/ore stockpiles, rotary drill gear, and animated Pixel Crawler NPCs.
    /// </summary>
    public static class IronHollowSceneBuilder
    {
        const string MainScenePath = "Assets/Scenes/Hub_IronHollow.unity";
        const string ArtScenePath = "Assets/Scenes/Hub_IronHollow_Art.unity";

        const string DungeonSheet = "Dungeon_Tiles";
        const string WallVarSheet = "Wall_Variations";

        const string FurnacePath = "Assets/Art/Environments/Stations/Furnace/Furnace.png";
        const string ToolsPath = "Assets/Art/Environments/Props/Static/Tools.png";
        const string ResourcesPath = "Assets/Art/Environments/Props/Static/Resources.png";
        const string RocksPath = "Assets/Art/Environments/Props/Static/Rocks.png";
        const string DungeonPropsPath = "Assets/Art/Environments/Props/Static/Dungeon_Props.png";
        const string ExtPropsPath = "Assets/Art/Environments/Buildings/Exterior/Props.png";
        const string IntPropsPath = "Assets/Art/Environments/Buildings/Interior/Interior_Props_01.png";
        const string BonfirePath = "Assets/Art/Environments/Stations/Bonfire/Bonfire_01-Sheet.png";
        const string FirePath = "Assets/Art/Environments/Stations/Bonfire/Fire_01-Sheet.png";
        const string WorkbenchPath = "Assets/Art/Environments/Stations/Workbench/Workbench.png";
        const string CustomLiftPath = "Assets/Art/Environments/Custom/DeepShaftLift.png";
        const string VillagePropsRoot = "Assets/Art/Environments/Village_Interiors/furniture_and_props_sprites/";
        const string MinerFSheet = "Assets/Art/Characters/NPCs/Citizen_F/Miner_F/Idle/Idle-Sheet.png";
        const string MinerFCtrl = "Assets/Animations/NPCs/NPC_CitizenF_MinerF.controller";
        const string MinerMSheet = "Assets/Art/Characters/NPCs/Citizen_M/Miner_M/Idle/Idle_Side-Sheet.png";
        const string MinerMCtrl = "Assets/Animations/NPCs/NPC_CitizenM_MinerM.controller";
        const string BarksPath = "Assets/Data/Dialogue/Case04/D_IndustrialBarks_04.asset";

        [MenuItem("Morsoul/Scenes/Iron Hollow", priority = 17)]
        public static void RebuildMenu() => Rebuild();

        public static void Rebuild()
        {
            PixelCrawlerHubToolkit.ClearCache();
            RebuildMainSceneBindings();
            AssetDatabase.SaveAssets();
            Debug.Log("IronHollowSceneBuilder: rebuilt unified Hub_IronHollow (Pixel Crawler).");
        }

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

                BuildMineTilemaps(scene);
                BuildMineProps(scene);

                PixelCrawlerHubToolkit.RebuildBounds(scene, "IronHollowBounds",
                    ("Bound_North", new Vector2(0f, 7.2f), new Vector2(48f, 1.4f)),
                    ("Bound_South", new Vector2(0f, -9.8f), new Vector2(48f, 1.4f)),
                    ("Bound_West", new Vector2(-21.8f, -1.0f), new Vector2(1.6f, 20.0f)),
                    ("Bound_East", new Vector2(21.8f, -1.0f), new Vector2(1.6f, 20.0f)));

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

        static void BuildMineTilemaps(Scene scene)
        {
            var grid = PixelCrawlerHubToolkit.EnsureGrid(scene, "EnvironmentGrid");
            var floorMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "MineFloor", -10, 2.0f);
            var gravelMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "MineGravel", -9, 1.99f);
            var deckMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "TimberDecks", -8, 1.98f);
            var cliffMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "CavernWalls", -6, 1.95f);

            var dirtTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Dirt");
            var cobbleTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Cobble");

            // Natural compacted-earth mine floor across expanded 46x22 cavern.
            PixelCrawlerHubToolkit.FillRect(floorMap, dirtTile, -28, -14, 28, 13);

            // Organic rough gravel/rocky terrain patches around excavation faces, haulage corridor, & east yard.
            PixelCrawlerHubToolkit.FillRect(gravelMap, cobbleTile, -22, -3, -11, 6);
            PixelCrawlerHubToolkit.FillRect(gravelMap, cobbleTile, -11, -1, 18, 1);
            PixelCrawlerHubToolkit.FillRect(gravelMap, cobbleTile, 10, -7, 21, 5);
            PixelCrawlerHubToolkit.FillRect(gravelMap, cobbleTile, -4, -9, 6, -5);

            // Worked timber-plank platforms strictly under Drill Pit, Central Forge, & Deep Shaft Hoist.
            StampWoodDeck(deckMap, -18, 2, 6, 4);
            StampWoodDeck(deckMap, -4, 3, 9, 4);
            StampWoodDeck(deckMap, 13, 2, 6, 4);

            // Northern craggy slate cavern cliff face (Wall_Variations slate r=10..12).
            for (int x = -28; x <= 28; x++)
            {
                for (int dy = 0; dy < 3; dy++)
                {
                    int r = 12 - dy;
                    int c = Mathf.Abs(x) % 8;
                    var wt = PixelCrawlerHubToolkit.LoadStaticTile(WallVarSheet, $"{WallVarSheet}_{r}_{c}");
                    if (wt != null) cliffMap.SetTile(new Vector3Int(x, 7 + dy, 0), wt);
                }
            }

            // West & East perimeter cavern rock walls enclosing the expanded mine.
            for (int y = -13; y <= 9; y++)
            {
                int r = y >= 7 ? 10 : (Mathf.Abs(y) % 2 == 0 ? 12 : 11);
                for (int wx = -26; wx <= -22; wx++)
                {
                    var wt = PixelCrawlerHubToolkit.LoadStaticTile(WallVarSheet, $"{WallVarSheet}_{r}_{Mathf.Abs(wx) % 8}");
                    if (wt != null) cliffMap.SetTile(new Vector3Int(wx, y, 0), wt);
                }
                for (int ex = 22; ex <= 26; ex++)
                {
                    var wt = PixelCrawlerHubToolkit.LoadStaticTile(WallVarSheet, $"{WallVarSheet}_{r}_{Mathf.Abs(ex) % 8}");
                    if (wt != null) cliffMap.SetTile(new Vector3Int(ex, y, 0), wt);
                }
            }

            // Three timber-braced drift tunnel portals carved into north cavern cliff (r=15..17, cols 12..15).
            StampTimberTunnel(cliffMap, startX: -17, baseY: 7);
            StampTimberTunnel(cliffMap, startX: -1, baseY: 7);
            StampTimberTunnel(cliffMap, startX: 14, baseY: 7);

            floorMap.RefreshAllTiles();
            gravelMap.RefreshAllTiles();
            deckMap.RefreshAllTiles();
            cliffMap.RefreshAllTiles();
        }

        static void StampWoodDeck(Tilemap deckMap, int xMin, int yMin, int width, int height)
        {
            for (int dx = 0; dx < width; dx++)
            {
                int c = dx == 0 ? 14 : (dx == width - 1 ? 16 : 15);
                for (int dy = 0; dy < height; dy++)
                {
                    int r = dy == height - 1 ? 0 : (dy == 0 ? 2 : 1);
                    var wt = PixelCrawlerHubToolkit.LoadStaticTile(DungeonSheet, $"{DungeonSheet}_{r}_{c}");
                    if (wt != null) deckMap.SetTile(new Vector3Int(xMin + dx, yMin + dy, 0), wt);
                }
            }
        }

        static void StampTimberTunnel(Tilemap cliffMap, int startX, int baseY)
        {
            for (int dx = 0; dx < 4; dx++)
            {
                int c = 12 + dx;
                for (int dy = 0; dy < 3; dy++)
                {
                    int r = 17 - dy;
                    var t = PixelCrawlerHubToolkit.LoadStaticTile(WallVarSheet, $"{WallVarSheet}_{r}_{c}");
                    if (t != null) cliffMap.SetTile(new Vector3Int(startX + dx, baseY + dy, 0), t);
                }
            }
        }

        static void BuildMineProps(Scene scene)
        {
            var root = PixelCrawlerHubToolkit.EnsurePropsRoot(scene, "EnvironmentProps").transform;

            var ironFurnace = PixelCrawlerHubToolkit.LoadSprite(FurnacePath, "Furnace_06");
            var brickFurnace = PixelCrawlerHubToolkit.LoadSprite(FurnacePath, "Furnace_03");
            var anvil = PixelCrawlerHubToolkit.LoadSprite(ToolsPath, "Tools_41");
            var largeAnvil = PixelCrawlerHubToolkit.LoadSprite(ToolsPath, "Tools_58");
            var grindstone = PixelCrawlerHubToolkit.LoadSprite(ToolsPath, "Tools_87");
            var quenchTrough = PixelCrawlerHubToolkit.LoadSprite(ToolsPath, "Tools_111");
            var choppingStump = PixelCrawlerHubToolkit.LoadSprite(ToolsPath, "Tools_50");
            var woodWorkbench = PixelCrawlerHubToolkit.LoadSprite(WorkbenchPath, "Workbench_Wood_Med");
            var steelWorkbench = PixelCrawlerHubToolkit.LoadSprite(WorkbenchPath, "Workbench_Steel_Med");
            var leverBank = PixelCrawlerHubToolkit.LoadSprite(DungeonPropsPath, "Dungeon_Props_00");
            var coalLarge = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_02");
            var coalCrate = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_35");
            var supplyCrate = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_34");
            var boulder = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_05");
            var boulderDark = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_01");
            var tallSpire = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_04");
            var spireStone = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_07");
            var pebbleStone = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_06");
            var rockCluster = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_11");
            var logBeam = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_21");
            var woodStool = AssetDatabase.LoadAssetAtPath<Sprite>(VillagePropsRoot + "big_round_table_chairs_0.png");
            var barrelA = AssetDatabase.LoadAssetAtPath<Sprite>(VillagePropsRoot + "barrels_5.png");
            var barrelB = AssetDatabase.LoadAssetAtPath<Sprite>(VillagePropsRoot + "barrels_6.png");
            var barrelCluster = AssetDatabase.LoadAssetAtPath<Sprite>(VillagePropsRoot + "barrels_7.png");

            // 1) North Smelter & Forge Plaza along northern cliff.
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Furnace_Main", ironFurnace,
                new Vector2(2.2f, 5.6f), colliderSize: new Vector2(2.2f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Furnace_Brick", brickFurnace,
                new Vector2(-1.8f, 5.6f), colliderSize: new Vector2(2.2f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Anvil_Central", anvil,
                new Vector2(4.6f, 4.6f), colliderSize: new Vector2(1.1f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Workbench_Central", steelWorkbench,
                new Vector2(-4.4f, 4.6f), colliderSize: new Vector2(2.6f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));

            // 2) West Excavation & Drill Pit stations.
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_DrillControls", leverBank,
                new Vector2(-15.0f, 5.4f), colliderSize: new Vector2(2.8f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Workbench_West", woodWorkbench,
                new Vector2(-11.0f, 4.6f), colliderSize: new Vector2(2.6f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Anvil_West", anvil,
                new Vector2(-12.2f, 3.2f), colliderSize: new Vector2(1.1f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));

            // 3) East Deep Shaft Hoist & Loading Yard stations.
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_LiftControls", leverBank,
                new Vector2(12.2f, 5.4f), colliderSize: new Vector2(2.8f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Workbench_East", steelWorkbench,
                new Vector2(8.8f, 4.6f), colliderSize: new Vector2(2.6f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));

            // 4) South Miners' Camp: flat resting stone + 3 wooden stools around RestSpot (-9.5, -5.5).
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_RestStone.png",
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var restStone = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_RestStone.png", 16f);
            if (restStone != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_CampRestStone", restStone,
                    new Vector2(-9.5f, -3.4f), colliderSize: new Vector2(1.8f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));
            if (woodStool != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_CampStool_W", woodStool, new Vector2(-11.1f, -5.5f), colliderSize: new Vector2(0.7f, 0.5f), colliderOffset: new Vector2(0f, 0.25f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_CampStool_E", woodStool, new Vector2(-7.9f, -5.5f), colliderSize: new Vector2(0.7f, 0.5f), colliderOffset: new Vector2(0f, 0.25f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_CampStool_S", woodStool, new Vector2(-10.3f, -6.9f), colliderSize: new Vector2(0.7f, 0.5f), colliderOffset: new Vector2(0f, 0.25f));
            }

            // Coal stockpiles & ore crates organized near mining faces, forge, & East storage yard.
            foreach (var pos in new[] {
                new Vector2(-6.8f, 5.9f), new Vector2(5.8f, 3.0f),
                new Vector2(-18.5f, 5.2f), new Vector2(16.2f, -3.2f),
                new Vector2(16.6f, -6.5f)
            })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_Coal_{pos.x:F0}_{pos.y:F0}", coalLarge, pos,
                    colliderSize: new Vector2(1.6f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));

            foreach (var pos in new[] {
                new Vector2(5.8f, 5.5f),
                new Vector2(13.8f, -5.2f), new Vector2(16.6f, -5.2f),
                new Vector2(-14.2f, -6.4f)
            })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_OreCrate_{pos.x:F0}_{pos.y:F0}", coalCrate, pos,
                    colliderSize: new Vector2(0.85f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));

            // Supply crates, timber beams & barrel clusters in SW Haulage Dock & East Storage Yard.
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Supply_SW1", supplyCrate, new Vector2(-15.4f, -6.5f), colliderSize: new Vector2(0.95f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Supply_SW2", coalCrate, new Vector2(-14.2f, -7.4f), colliderSize: new Vector2(0.85f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Supply_SE1", supplyCrate, new Vector2(13.8f, -6.4f), colliderSize: new Vector2(0.95f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Supply_SE2", supplyCrate, new Vector2(15.0f, -7.3f), colliderSize: new Vector2(0.95f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));
            if (logBeam != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_TimberStack_W", logBeam, new Vector2(-13.8f, -1.6f), colliderSize: new Vector2(1.8f, 0.45f), colliderOffset: new Vector2(0f, 0.22f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_TimberStack_E", logBeam, new Vector2(17.8f, -6.8f), colliderSize: new Vector2(1.8f, 0.45f), colliderOffset: new Vector2(0f, 0.22f));
            }
            if (barrelA != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Barrels_SW", barrelA, new Vector2(-16.6f, -6.2f), colliderSize: new Vector2(1.1f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));
            if (barrelCluster != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Barrels_SE", barrelCluster, new Vector2(18.2f, -4.8f), colliderSize: new Vector2(1.4f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));
            if (barrelB != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Barrels_Forge", barrelB, new Vector2(-6.8f, 4.5f), colliderSize: new Vector2(1.0f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));

            // Natural cavern rock formations framing the expanded perimeter & rocky edges.
            foreach (var pos in new[] {
                new Vector2(-20.8f, 4.8f), new Vector2(-20.8f, -0.5f), new Vector2(-20.8f, -5.8f), new Vector2(-19.2f, -8.2f),
                new Vector2(20.8f, 4.5f), new Vector2(20.8f, -0.8f), new Vector2(20.8f, -5.8f), new Vector2(19.8f, 2.2f),
                new Vector2(-7.0f, -8.5f), new Vector2(-2.2f, -8.5f), new Vector2(7.5f, -8.5f), new Vector2(18.5f, -8.3f),
                new Vector2(-12.2f, -8.4f), new Vector2(12.8f, -8.5f), new Vector2(-8.8f, 5.8f), new Vector2(9.6f, 2.2f)
            })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_Rock_{pos.x:F0}_{pos.y:F0}", boulder ?? boulderDark, pos,
                    colliderSize: new Vector2(1.35f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));

            if (tallSpire != null)
            {
                foreach (var pos in new[] { new Vector2(-20.6f, 1.5f), new Vector2(20.6f, -3.2f), new Vector2(5.2f, -8.4f), new Vector2(-16.2f, -8.4f) })
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_TallSpire_{pos.x:F0}_{pos.y:F0}", tallSpire, pos,
                        colliderSize: new Vector2(1.4f, 0.9f), colliderOffset: new Vector2(0f, 0.45f));
            }
            if (spireStone != null)
            {
                foreach (var pos in new[] {
                    new Vector2(-20.4f, -3.2f), new Vector2(20.4f, 1.8f), new Vector2(2.4f, -8.6f),
                    new Vector2(-8.0f, -7.2f), new Vector2(10.8f, -6.4f), new Vector2(19.4f, -0.5f)
                })
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_Spire_{pos.x:F0}_{pos.y:F0}", spireStone, pos,
                        colliderSize: new Vector2(1.2f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));
            }
            if (pebbleStone != null)
            {
                foreach (var pos in new[] {
                    new Vector2(-14.6f, 0.8f), new Vector2(-5.4f, -2.2f), new Vector2(3.6f, -3.8f),
                    new Vector2(8.8f, -2.1f), new Vector2(15.6f, -1.6f), new Vector2(-6.2f, -7.2f)
                })
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_Pebble_{pos.x:F0}_{pos.y:F0}", pebbleStone, pos, sortingOrder: -6);
            }
            if (rockCluster != null)
            {
                foreach (var pos in new[] { new Vector2(-13.8f, 1.6f), new Vector2(9.2f, -3.8f), new Vector2(-4.2f, -4.8f), new Vector2(10.2f, -2.0f) })
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_Cluster_{pos.x:F0}_{pos.y:F0}", rockCluster, pos,
                        colliderSize: new Vector2(0.9f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
            }

            // Mining & smithing equipment from Tools.png (Grindstone, Quench Trough, Large Anvil, Chopping Stump).
            if (grindstone != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Grindstone_Forge", grindstone, new Vector2(6.4f, 4.1f), colliderSize: new Vector2(0.8f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Grindstone_West", grindstone, new Vector2(-10.2f, 2.7f), colliderSize: new Vector2(0.8f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
            }
            if (quenchTrough != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_QuenchTrough", quenchTrough, new Vector2(-0.2f, 3.6f), colliderSize: new Vector2(0.9f, 1.4f), colliderOffset: new Vector2(0f, 0.7f));
            if (largeAnvil != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_LargeAnvil_Forge", largeAnvil, new Vector2(-6.6f, 2.9f), colliderSize: new Vector2(1.4f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));
            if (choppingStump != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Stump_W", choppingStump, new Vector2(-14.2f, -3.1f), colliderSize: new Vector2(0.9f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Stump_E", choppingStump, new Vector2(16.6f, -7.2f), colliderSize: new Vector2(0.9f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
            }

            // Tool racks, giant shipping crates, iron ore crates, & mine lantern posts.
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_ToolRack.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_GiantCrate.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_IronOreCrate.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_LanternPost.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var toolRack = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_ToolRack.png", 16f);
            var giantCrate = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_GiantCrate.png", 16f);
            var ironOreCrate = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_IronOreCrate.png", 16f);
            var lanternPost = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_LanternPost.png", 16f);

            if (toolRack != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_ToolRack_W", toolRack, new Vector2(-8.5f, 4.6f), colliderSize: new Vector2(1.8f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_ToolRack_C", toolRack, new Vector2(7.0f, 5.4f), colliderSize: new Vector2(1.8f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_ToolRack_E", toolRack, new Vector2(10.6f, 4.8f), colliderSize: new Vector2(1.8f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));
            }
            if (giantCrate != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_GiantCrate_SW", giantCrate, new Vector2(-18.2f, -6.8f), colliderSize: new Vector2(1.9f, 1.1f), colliderOffset: new Vector2(0f, 0.55f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_GiantCrate_SE1", giantCrate, new Vector2(11.8f, -5.8f), colliderSize: new Vector2(1.9f, 1.1f), colliderOffset: new Vector2(0f, 0.55f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_GiantCrate_SE2", giantCrate, new Vector2(16.7f, -2.2f), colliderSize: new Vector2(1.9f, 1.1f), colliderOffset: new Vector2(0f, 0.55f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_GiantCrate_Forge", giantCrate, new Vector2(-7.8f, 2.0f), colliderSize: new Vector2(1.9f, 1.1f), colliderOffset: new Vector2(0f, 0.55f));
            }
            if (ironOreCrate != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_IronCrate_W", ironOreCrate, new Vector2(-13.8f, 2.3f), colliderSize: new Vector2(0.85f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_IronCrate_C", ironOreCrate, new Vector2(6.9f, 2.9f), colliderSize: new Vector2(0.85f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_IronCrate_SW", ironOreCrate, new Vector2(-13.0f, -6.4f), colliderSize: new Vector2(0.85f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_IronCrate_SE1", ironOreCrate, new Vector2(15.2f, -5.2f), colliderSize: new Vector2(0.85f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_IronCrate_SE2", ironOreCrate, new Vector2(15.2f, -6.4f), colliderSize: new Vector2(0.85f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));
            }
            if (lanternPost != null)
            {
                foreach (var pos in new[] {
                    new Vector2(-10.0f, 1.1f), new Vector2(-3.5f, 1.1f), new Vector2(7.5f, 1.1f),
                    new Vector2(13.2f, -1.2f), new Vector2(-13.8f, -4.6f)
                })
                {
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_Lantern_{pos.x:F0}_{pos.y:F0}", lanternPost, pos,
                        colliderSize: new Vector2(0.5f, 0.4f), colliderOffset: new Vector2(0f, 0.2f));
                    PixelCrawlerHubToolkit.PlaceSoftGlow(root, $"Iron_LanternGlow_{pos.x:F0}_{pos.y:F0}",
                        pos + new Vector2(0.15f, 0.95f), new Color(1.0f, 0.72f, 0.32f, 0.24f), scale: 2.8f);
                }
            }

            // Carrier Wagon staged at the SW Supply & Haulage Dock.
            var carrierWagon = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/Carrier_Wagon.png", 16f);
            if (carrierWagon != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Carrier_Wagon", carrierWagon,
                    new Vector2(-18.0f, -4.2f), colliderSize: new Vector2(4.2f, 1.4f), colliderOffset: new Vector2(0f, 0.7f));

            // Continuous east-west mine railway (y=0.0) + East Storage southern spur (x=12.0, y=-1.5..-3.5) + aligned minecarts.
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_Rail_H.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_Rail_V.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_Minecart_Empty.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_Minecart_Iron.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_Minecart_Coal.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var railH = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_Rail_H.png", 16f);
            var railV = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_Rail_V.png", 16f);
            if (railH != null)
            {
                for (int rx = -14; rx <= 14; rx += 2)
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_Rail_H_{rx}", railH, new Vector2(rx, 0.0f), zBias: 0f, sortingOrder: -7);
            }
            if (railV != null)
            {
                foreach (float ry in new[] { -1.5f, -3.5f })
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_Rail_V_{ry:F1}", railV, new Vector2(12.0f, ry), zBias: 0f, sortingOrder: -7);
            }
            var cartEmpty = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_Minecart_Empty.png", 16f);
            var cartIron = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_Minecart_Iron.png", 16f);
            var cartCoal = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_Minecart_Coal.png", 16f);
            if (cartEmpty != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Minecart_West", cartEmpty, new Vector2(-11.5f, 0.25f), colliderSize: new Vector2(1.6f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));
            if (cartIron != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Minecart_Center", cartIron, new Vector2(-1.5f, 0.25f), colliderSize: new Vector2(1.6f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));
            if (cartCoal != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Minecart_East", cartCoal, new Vector2(9.5f, 0.25f), colliderSize: new Vector2(1.6f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));

            // Varied Iron Ore (Small, Medium, Large) & Coal deposits integrated into cavern rock faces.
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_IronOre_S.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_IronOre_M.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_IronOre_L.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_CoalOre_M.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var ironOreS = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_IronOre_S.png", 16f);
            var ironOreM = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_IronOre_M.png", 16f);
            var ironOreL = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_IronOre_L.png", 16f);
            var coalOreM = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_CoalOre_M.png", 16f);

            if (ironOreL != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Deposit_L_W", ironOreL, new Vector2(-19.2f, 2.8f), colliderSize: new Vector2(2.2f, 1.1f), colliderOffset: new Vector2(0f, 0.55f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Deposit_L_S", ironOreL, new Vector2(0.8f, -7.8f), colliderSize: new Vector2(2.2f, 1.1f), colliderOffset: new Vector2(0f, 0.55f));
            }
            if (ironOreM != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Deposit_M_NW", ironOreM, new Vector2(-17.4f, 1.0f), colliderSize: new Vector2(1.6f, 0.85f), colliderOffset: new Vector2(0f, 0.42f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Deposit_M_SE", ironOreM, new Vector2(19.2f, -2.2f), colliderSize: new Vector2(1.6f, 0.85f), colliderOffset: new Vector2(0f, 0.42f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Deposit_M_S", ironOreM, new Vector2(-4.2f, -7.6f), colliderSize: new Vector2(1.6f, 0.85f), colliderOffset: new Vector2(0f, 0.42f));
            }
            if (ironOreS != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Deposit_S_W1", ironOreS, new Vector2(-15.8f, 1.2f), colliderSize: new Vector2(0.9f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Deposit_S_C", ironOreS, new Vector2(4.2f, -6.2f), colliderSize: new Vector2(0.9f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Deposit_S_E", ironOreS, new Vector2(19.0f, 0.6f), colliderSize: new Vector2(0.9f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
            }
            if (coalOreM != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Coal_Deposit_M_W", coalOreM, new Vector2(-19.4f, -1.8f), colliderSize: new Vector2(1.4f, 0.75f), colliderOffset: new Vector2(0f, 0.38f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Coal_Deposit_M_S", coalOreM, new Vector2(-1.6f, -6.5f), colliderSize: new Vector2(1.4f, 0.75f), colliderOffset: new Vector2(0f, 0.38f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Coal_Deposit_M_E", coalOreM, new Vector2(19.2f, -3.6f), colliderSize: new Vector2(1.4f, 0.75f), colliderOffset: new Vector2(0f, 0.38f));
            }

            // Sealed non-functional blasting supplies (powder barrels & dynamite crates).
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_PowderBarrel.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_DynamiteCrate.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var powderBarrel = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_PowderBarrel.png", 16f);
            var dynamiteCrate = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_DynamiteCrate.png", 16f);
            if (powderBarrel != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_PowderBarrel_W", powderBarrel, new Vector2(-17.3f, 4.1f), colliderSize: new Vector2(0.85f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_PowderBarrel_E", powderBarrel, new Vector2(13.6f, -3.5f), colliderSize: new Vector2(0.85f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
            }
            if (dynamiteCrate != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_DynamiteCrate_W", dynamiteCrate, new Vector2(-16.1f, 4.1f), colliderSize: new Vector2(1.0f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_DynamiteCrate_E", dynamiteCrate, new Vector2(14.7f, -3.6f), colliderSize: new Vector2(1.0f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));
            }

            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "Iron_ForgeGlow", new Vector2(0.2f, 5.2f), new Color(1.0f, 0.55f, 0.18f, 0.34f), scale: 5.0f);
            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "Iron_CampGlow", new Vector2(-9.5f, -5.2f), new Color(1.0f, 0.65f, 0.25f, 0.32f), scale: 3.8f);
        }

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
                var oldEnv = PixelCrawlerHubToolkit.FindRoot(scene, "Environment");
                if (oldEnv != null) Object.DestroyImmediate(oldEnv);
                var oldGrid = PixelCrawlerHubToolkit.FindRoot(scene, "EnvironmentGrid");
                if (oldGrid != null) Object.DestroyImmediate(oldGrid);
                BuildMineTilemaps(scene);
                BuildMineProps(scene);
                PixelCrawlerHubToolkit.RebuildBounds(scene, "IronHollowBounds",
                    ("Bound_North", new Vector2(0f, 7.2f), new Vector2(40f, 1.4f)),
                    ("Bound_South", new Vector2(0f, -7.8f), new Vector2(40f, 1.4f)),
                    ("Bound_West", new Vector2(-18.0f, -0.6f), new Vector2(1.6f, 17.5f)),
                    ("Bound_East", new Vector2(17.8f, -0.6f), new Vector2(1.6f, 17.5f)));
                PixelCrawlerHubToolkit.ConfigureCamera(
                    scene, new Vector2(-23.0f, -11.0f), new Vector2(23.0f, 9.0f),
                    5.5f, new Color(0.06f, 0.06f, 0.08f, 1f));

                var playerProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Player", "Assets/Art/Characters/Player/Golem/Idle/Idle_Down-Sheet.png",
                    "Idle_Down-Sheet_0", "Assets/Animations/Player/Player_Golem.controller",
                    new Vector2(0f, -0.35f));
                var thrunProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Thrun", "Assets/Art/Characters/NPCs/Citizen_M/Smith_A/Idle/Idle_Side-Sheet.png",
                    "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_SmithA.controller",
                    new Vector2(0f, -0.35f), flipX: true);
                var harlanProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Harlan", "Assets/Art/Characters/NPCs/Named/Harlan/Idle/Idle_Down-Sheet.png",
                    "Idle_Down-Sheet_0", "Assets/Animations/NPCs/NPC_Harlan.controller",
                    new Vector2(0f, -0.35f), flipX: true);
                var minerProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "MinerF", MinerFSheet, "Idle-Sheet_0", MinerFCtrl,
                    new Vector2(0f, -0.35f), flipX: true);
                var carrierProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Carrier", "Assets/Art/Characters/NPCs/Named/Carrier/Idle/Idle_Down-Sheet.png",
                    "Idle_Down-Sheet_0", "Assets/Animations/NPCs/NPC_Carrier.controller",
                    new Vector2(0f, -0.35f));

                SetRootPos(scene, "Player", new Vector3(0f, -2.5f, 0f));
                SetRootPos(scene, "Carrier", new Vector3(-15.5f, -4.8f, 0f));
                SetRootPos(scene, "RestSpot", new Vector3(-9.5f, -5.5f, 0f));
                SetRootPos(scene, "DrillBit", new Vector3(-15.0f, 4.0f, 0f));
                SetRootPos(scene, "Thrun", new Vector3(2.6f, 3.6f, 0f));
                SetRootPos(scene, "Harlan", new Vector3(5.2f, 1.4f, 0f));
                SetRootPos(scene, "MinerCrowd", new Vector3(-7.2f, -6.2f, 0f));
                SetRootPos(scene, "DeepShaftLift", new Vector3(16.0f, 4.2f, 0f));

                PixelCrawlerHubToolkit.BindRoot(scene, "Player", playerProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "Thrun", thrunProf);
                ForceSprite(scene, "Thrun", thrunProf.sprite);
                PixelCrawlerHubToolkit.BindRoot(scene, "Harlan", harlanProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "MinerCrowd", minerProf);
                ForceSprite(scene, "MinerCrowd", minerProf.sprite);
                PixelCrawlerHubToolkit.BindRoot(scene, "Carrier", carrierProf);

                var barkDialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(BarksPath);
                EnsureAmbientNpc(scene, "Iron_Watchman", new Vector3(18.6f, 1.8f, 0f),
                    "Assets/Art/Characters/NPCs/Citizen_M/Guard_A/Idle/Idle_Side-Sheet.png",
                    "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_GuardA.controller",
                    true, barkDialogue);

                // Populate expanded mining cavern with male & female miners across the 4 zones.
                EnsureAmbientNpc(scene, "Miner_Camp_M", new Vector3(-11.8f, -4.4f, 0f),
                    MinerMSheet, "Idle_Side-Sheet_0", MinerMCtrl, false, barkDialogue);
                EnsureAmbientNpc(scene, "Miner_West_M", new Vector3(-13.2f, 1.8f, 0f),
                    MinerMSheet, "Idle_Side-Sheet_0", MinerMCtrl, true, barkDialogue);
                EnsureAmbientNpc(scene, "Miner_West_F", new Vector3(-18.2f, -0.5f, 0f),
                    MinerFSheet, "Idle-Sheet_0", MinerFCtrl, false, barkDialogue);
                EnsureAmbientNpc(scene, "Miner_Central_F", new Vector3(-3.6f, -2.2f, 0f),
                    MinerFSheet, "Idle-Sheet_0", MinerFCtrl, false, barkDialogue);
                EnsureAmbientNpc(scene, "Miner_South_M", new Vector3(2.4f, -5.4f, 0f),
                    MinerMSheet, "Idle_Side-Sheet_0", MinerMCtrl, true, barkDialogue);
                EnsureAmbientNpc(scene, "Miner_East_F", new Vector3(12.2f, 2.2f, 0f),
                    MinerFSheet, "Idle-Sheet_0", MinerFCtrl, false, barkDialogue);
                EnsureAmbientNpc(scene, "Miner_East_M", new Vector3(10.2f, -3.4f, 0f),
                    MinerMSheet, "Idle_Side-Sheet_0", MinerMCtrl, false, barkDialogue);

                AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/IronHollow_ToolCart_Drill.png", ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var toolCart = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/IronHollow_ToolCart_Drill.png", 16f);
                if (toolCart != null)
                {
                    PixelCrawlerHubToolkit.BindStaticProp(scene, "DrillBit", "Iron_DrillRig", "Assets/Art/Environments/Custom/IronHollow_ToolCart_Drill.png", toolCart.name);
                    ForceSprite(scene, "DrillBit", toolCart);
                }
                else
                {
                    PixelCrawlerHubToolkit.BindStaticProp(scene, "DrillBit", "Iron_DrillBlade", ToolsPath, "Tools_25");
                }
                PixelCrawlerHubToolkit.BindStaticProp(scene, "RestSpot", "Iron_RestCampfire", BonfirePath, "Bonfire_01-Sheet_0");
                AttachCampfireFlame(scene, "RestSpot");
                PixelCrawlerHubToolkit.BindStaticProp(scene, "DeepShaftLift", "Iron_ShaftLiftGate", CustomLiftPath, "DeepShaftLift");

                EnsureSolidBody(scene, "DrillBit", new Vector2(1.1f, 0.7f), new Vector2(0f, 0.35f));
                EnsureSolidBody(scene, "RestSpot", new Vector2(1.4f, 0.9f), new Vector2(0f, 0.45f));
                EnsureSolidBody(scene, "DeepShaftLift", new Vector2(2.6f, 1.2f), new Vector2(0f, 0.7f));

                PixelCrawlerHubToolkit.HideTriggerVisual(scene, "CaseDoor");
                var caseDoor = PixelCrawlerHubToolkit.FindRoot(scene, "CaseDoor");
                if (caseDoor != null) caseDoor.SetActive(false);

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

        static void SetRootPos(Scene scene, string name, Vector3 pos)
        {
            var go = PixelCrawlerHubToolkit.FindRoot(scene, name);
            if (go != null) go.transform.position = pos;
        }

        static void ForceSprite(Scene scene, string name, Sprite sprite)
        {
            var go = PixelCrawlerHubToolkit.FindRoot(scene, name);
            if (go == null || sprite == null) return;
            var sr = go.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null) sr.sprite = sprite;
        }

        static void AttachCampfireFlame(Scene scene, string rootName)
        {
            var go = PixelCrawlerHubToolkit.FindRoot(scene, rootName);
            if (go == null) return;
            var flameSprite = PixelCrawlerHubToolkit.LoadSprite(FirePath, "Fire_01-Sheet_0");
            if (flameSprite == null) return;

            var child = go.transform.Find("CampfireFlame");
            var flameGo = child != null ? child.gameObject : new GameObject("CampfireFlame");
            if (child == null) flameGo.transform.SetParent(go.transform, false);
            flameGo.transform.localPosition = new Vector3(0f, 0.3f, -0.01f);

            var sr = flameGo.GetComponent<SpriteRenderer>();
            if (sr == null) sr = flameGo.AddComponent<SpriteRenderer>();
            sr.sprite = flameSprite;
            sr.sortingOrder = 2;
        }

        static void EnsureSolidBody(Scene scene, string rootName, Vector2 size, Vector2 offset)
        {
            var go = PixelCrawlerHubToolkit.FindRoot(scene, rootName);
            if (go == null) return;
            var child = go.transform.Find("SolidBody");
            var body = child != null ? child.gameObject : new GameObject("SolidBody");
            if (child == null) body.transform.SetParent(go.transform, false);
            var col = body.GetComponent<BoxCollider2D>();
            if (col == null) col = body.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = size;
            col.offset = offset;
        }

        static void EnsureAmbientNpc(
            Scene scene, string name, Vector3 pos,
            string sheetPath, string spriteName, string ctrlPath,
            bool flipX, DialogueData dialogue)
        {
            var existing = PixelCrawlerHubToolkit.FindRoot(scene, name);
            if (existing == null)
            {
                HubSceneFactory.CreateInteractable(name, new Color(0.55f, 0.55f, 0.65f), pos, out NpcInteractable npc);
                npc.promptText = "[E] Talk";
                npc.dialogue = dialogue;
            }
            else
            {
                existing.transform.position = pos;
                var npc = existing.GetComponent<NpcInteractable>();
                if (npc != null && dialogue != null) npc.dialogue = dialogue;
            }

            var prof = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                name, sheetPath, spriteName, ctrlPath, new Vector2(0f, -0.35f), flipX);
            PixelCrawlerHubToolkit.BindRoot(scene, name, prof);
        }
    }
}

