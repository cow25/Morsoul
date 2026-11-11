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
        const string FurniturePath = "Assets/Art/Environments/Props/Static/Furniture.png";
        const string RocksPath = "Assets/Art/Environments/Props/Static/Rocks.png";
        const string DungeonPropsPath = "Assets/Art/Environments/Props/Static/Dungeon_Props.png";
        const string ExtPropsPath = "Assets/Art/Environments/Buildings/Exterior/Props.png";
        const string IntPropsPath = "Assets/Art/Environments/Buildings/Interior/Interior_Props_01.png";

        [MenuItem("Morsoul/Scenes/Iron Hollow", priority = 17)]
        public static void RebuildMenu() => Rebuild();

        public static void Rebuild()
        {
            PixelCrawlerHubToolkit.ClearCache();
            RebuildArtOverlay();
            RebuildMainSceneBindings();
            AssetDatabase.SaveAssets();
            Debug.Log("IronHollowSceneBuilder: rebuilt Hub_IronHollow and Hub_IronHollow_Art (Pixel Crawler).");
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
                    ("Bound_North", new Vector2(0f, 4.3f), new Vector2(18f, 1.4f)),
                    ("Bound_South", new Vector2(0f, -4.3f), new Vector2(18f, 1.4f)),
                    ("Bound_West", new Vector2(-8.1f, 0f), new Vector2(1.4f, 11f)),
                    ("Bound_East", new Vector2(8.1f, 0f), new Vector2(1.4f, 11f)));

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
            var deckMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "TimberDecks", -9, 1.99f);
            var cliffMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "CavernWalls", -6, 1.95f);

            // Slate mine cavern flagstones across main yard.
            PixelCrawlerHubToolkit.FillRandomStaticBlock(floorMap, DungeonSheet, -9, -5, 9, 4, 0, 2, 4, 7);

            // Framed wooden mine work platforms under DrillBit wing & DeepShaftLift boarding bay.
            StampWoodDeck(deckMap, -6, 0, 3, 3);
            StampWoodDeck(deckMap, 4, 0, 4, 3);

            // Northern craggy slate cavern cliff face (Wall_Variations slate r=10..12).
            for (int x = -9; x <= 9; x++)
            {
                for (int dy = 0; dy < 3; dy++)
                {
                    int r = 12 - dy;
                    int c = Mathf.Abs(x) % 8;
                    var wt = PixelCrawlerHubToolkit.LoadStaticTile(WallVarSheet, $"{WallVarSheet}_{r}_{c}");
                    if (wt != null) cliffMap.SetTile(new Vector3Int(x, 4 + dy, 0), wt);
                }
            }

            // Two timber-braced drift tunnel portals carved into north cavern cliff (r=15..17, cols 12..15).
            StampTimberTunnel(cliffMap, startX: -6, baseY: 4);
            StampTimberTunnel(cliffMap, startX: 4, baseY: 4);

            floorMap.RefreshAllTiles();
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
            var leverBank = PixelCrawlerHubToolkit.LoadSprite(DungeonPropsPath, "Dungeon_Props_00");
            var coalLarge = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_02");
            var coalCrate = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_35");
            var supplyCrates = PixelCrawlerHubToolkit.LoadSprite(FurniturePath, "Furniture_13");
            var lampPost = PixelCrawlerHubToolkit.LoadSprite(FurniturePath, "Furniture_52");
            var boulder = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_05");
            var boulderDark = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_01");

            // Glowing smelter foundry along center stone cliff between the two timber mine portals (clear of Thrun at 2,2).
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Furnace_Main", ironFurnace,
                new Vector2(1.4f, 3.2f), colliderSize: new Vector2(2.4f, 0.9f), colliderOffset: new Vector2(0f, -0.35f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Furnace_Brick", brickFurnace,
                new Vector2(-1.4f, 3.2f), colliderSize: new Vector2(2.4f, 0.9f), colliderOffset: new Vector2(0f, -0.35f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Anvil", anvil,
                new Vector2(3.3f, 2.7f), colliderSize: new Vector2(1.2f, 0.7f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_DrillControls", leverBank,
                new Vector2(-4.5f, 2.2f));

            // Coal & iron ore stockpiles around foundry and perimeter.
            foreach (var pos in new[] { new Vector2(-2.9f, 2.6f), new Vector2(3.5f, 1.8f), new Vector2(-6.8f, -2.5f), new Vector2(6.5f, -2.4f) })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_Coal_{pos.x:F0}_{pos.y:F0}", coalLarge, pos);

            foreach (var pos in new[] { new Vector2(-0.5f, -3.1f), new Vector2(4.2f, 3.1f), new Vector2(-6.3f, 2.6f) })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_OreCrate_{pos.x:F0}_{pos.y:F0}", coalCrate, pos);

            // Crates & mine lanterns lighting cavern corners.
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Supply_SW", supplyCrates,
                new Vector2(-5.2f, -3.0f), colliderSize: new Vector2(1.8f, 0.8f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Supply_SE", supplyCrates,
                new Vector2(5.2f, -3.0f), colliderSize: new Vector2(1.8f, 0.8f));

            foreach (var pos in new[] { new Vector2(-7.3f, 3.0f), new Vector2(7.3f, 3.0f), new Vector2(-7.1f, -2.1f), new Vector2(7.1f, -2.1f) })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_Lantern_{pos.x:F0}_{pos.y:F0}", lampPost, pos);

            // Slag boulders framing cavern flanks.
            foreach (var pos in new[] { new Vector2(-8.0f, 1.3f), new Vector2(-8.0f, -1.2f), new Vector2(8.0f, -0.4f), new Vector2(1.6f, -3.3f), new Vector2(-1.9f, -3.3f) })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_Rock_{pos.x:F0}_{pos.y:F0}", boulder ?? boulderDark, pos);
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
                PixelCrawlerHubToolkit.ConfigureCamera(
                    scene, new Vector2(-8f, -4.4f), new Vector2(8f, 4.7f),
                    5.2f, new Color(0.06f, 0.06f, 0.08f, 1f));

                var playerProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Player", "Assets/Art/Characters/Player/Body_A/Idle_Base/Idle_Down-Sheet.png",
                    "Idle_Down-Sheet_0", "Assets/Animations/Player/Player_BodyA.controller",
                    new Vector2(0f, -0.35f));
                var thrunProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Thrun", "Assets/Art/Characters/NPCs/Knight/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_Knight.controller",
                    new Vector2(0f, -0.35f), flipX: true);
                var harlanProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Harlan", "Assets/Art/Characters/NPCs/Citizen_F/Tavern_B/Idle/Idle_Side-Sheet.png",
                    "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_TavernB.controller",
                    new Vector2(0f, -0.35f), flipX: true);
                var minerProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Eliza", "Assets/Art/Characters/NPCs/Citizen_F/Peasant_A/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_PeasantA.controller",
                    new Vector2(0f, -0.35f));
                var carrierProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Carrier", "Assets/Art/Characters/NPCs/Rogue/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_Rogue.controller",
                    new Vector2(0f, -0.35f));

                PixelCrawlerHubToolkit.BindRoot(scene, "Player", playerProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "Thrun", thrunProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "Harlan", harlanProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "MinerCrowd", minerProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "Carrier", carrierProf);

                var restSpot = PixelCrawlerHubToolkit.FindRoot(scene, "RestSpot");
                if (restSpot != null) restSpot.transform.position = new Vector3(-6.4f, 0.3f, 0f);

                PixelCrawlerHubToolkit.BindStaticProp(scene, "DrillBit", "Iron_DrillBlade", ToolsPath, "Tools_25");
                PixelCrawlerHubToolkit.BindStaticProp(scene, "RestSpot", "Iron_RestBench", ExtPropsPath, "Ext_Props_09");
                PixelCrawlerHubToolkit.BindStaticProp(scene, "DeepShaftLift", "Iron_ShaftLiftGate", IntPropsPath, "Int_Interior_Props_01_45");

                PixelCrawlerHubToolkit.HideTriggerVisual(scene, "CaseDoor");

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
    }
}

