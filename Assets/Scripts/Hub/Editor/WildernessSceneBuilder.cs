using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Rebuilds Hub_Wilderness and its art overlay Hub_Wilderness_Art using
    /// Pixel Crawler forest/water/dirt RuleTiles, evergreen &amp; oak trees,
    /// ranger campfire outpost, and animated Pixel Crawler NPCs.
    /// </summary>
    public static class WildernessSceneBuilder
    {
        const string MainScenePath = "Assets/Scenes/Hub_Wilderness.unity";
        const string ArtScenePath = "Assets/Scenes/Hub_Wilderness_Art.unity";

        const string TreeM1Path = "Assets/Art/Environments/Props/Trees/Model_01/Size_03.png";
        const string TreeM2Path = "Assets/Art/Environments/Props/Trees/Model_02/Size_03.png";
        const string TreeM3Path = "Assets/Art/Environments/Props/Trees/Model_03/Size_03.png";
        const string VegPath = "Assets/Art/Environments/Props/Static/Vegetation.png";
        const string RocksPath = "Assets/Art/Environments/Props/Static/Rocks.png";
        const string FurniturePath = "Assets/Art/Environments/Props/Static/Furniture.png";
        const string FarmPath = "Assets/Art/Environments/Props/Static/Farm.png";
        const string BonfirePath = "Assets/Art/Environments/Stations/Bonfire/Bonfire.png";
        const string DungeonPropsPath = "Assets/Art/Environments/Props/Static/Dungeon_Props.png";

        [MenuItem("Morsoul/Scenes/Wilderness", priority = 15)]
        public static void RebuildMenu() => Rebuild();

        public static void Rebuild()
        {
            PixelCrawlerHubToolkit.ClearCache();
            RebuildArtOverlay();
            RebuildMainSceneBindings();
            AssetDatabase.SaveAssets();
            Debug.Log("WildernessSceneBuilder: rebuilt Hub_Wilderness and Hub_Wilderness_Art (Pixel Crawler).");
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

                BuildTerrain(scene);
                BuildProps(scene);
                PixelCrawlerHubToolkit.RebuildBounds(scene, "WildernessBounds",
                    ("Bound_North", new Vector2(0f, 4.4f), new Vector2(18f, 1.6f)),
                    ("Bound_South", new Vector2(0f, -4.4f), new Vector2(18f, 1.4f)),
                    ("Bound_West", new Vector2(-8.0f, 0f), new Vector2(1.4f, 11f)),
                    ("Bound_East", new Vector2(8.0f, 0f), new Vector2(1.4f, 11f)));

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

        static void BuildTerrain(Scene scene)
        {
            var grid = PixelCrawlerHubToolkit.EnsureGrid(scene, "EnvironmentGrid");
            var grassTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Grass");
            var dirtTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Dirt");
            var snowTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Snow");
            var waterTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Water");

            var grassMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "GroundGrass", -10, 2.0f);
            var dirtMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "GroundDirt", -9, 1.99f);
            var snowMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "HighlandFrost", -8, 1.98f);
            var waterMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "ForestPond", -7, 1.96f, addCollider: true);

            PixelCrawlerHubToolkit.FillRect(grassMap, grassTile, -18, -12, 18, 12);

            // Whisperwood east-west trail from TrailExit (-6,0) to BriarGate (6,0.5) + camp clearing.
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -14, -1, 12, 1);
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -6, -3, -3, -1);

            // Light highland frost along distant northern ridge.
            PixelCrawlerHubToolkit.FillRect(snowMap, snowTile, -14, 7, 14, 11);

            // Woodland pond in SE corner (collider keeps player on shore).
            PixelCrawlerHubToolkit.FillRect(waterMap, waterTile, 3, -6, 7, -3);

            grassMap.RefreshAllTiles();
            dirtMap.RefreshAllTiles();
            snowMap.RefreshAllTiles();
            waterMap.RefreshAllTiles();
        }

        static void BuildProps(Scene scene)
        {
            var root = PixelCrawlerHubToolkit.EnsurePropsRoot(scene, "EnvironmentProps").transform;

            var oakGreen = PixelCrawlerHubToolkit.LoadSprite(TreeM1Path, "Tree_Model_01_Size_03_00");
            var oakGold = PixelCrawlerHubToolkit.LoadSprite(TreeM1Path, "Tree_Model_01_Size_03_01");
            var pineTeal = PixelCrawlerHubToolkit.LoadSprite(TreeM2Path, "Tree_Model_02_Size_03_00");
            var pineDark = PixelCrawlerHubToolkit.LoadSprite(TreeM2Path, "Tree_Model_02_Size_03_01");
            var spruceTall = PixelCrawlerHubToolkit.LoadSprite(TreeM3Path, "Tree_Model_03_Size_03_00");
            var bushMed = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_04");
            var fern = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_12");
            var flower = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_34");
            var boulder = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_05");
            var lampPost = PixelCrawlerHubToolkit.LoadSprite(FurniturePath, "Furniture_52");
            var sack = PixelCrawlerHubToolkit.LoadSprite(FarmPath, "Farm_06");

            // Dense Northern & Flanking Whisperwood forest perimeter.
            for (int i = 0; i < 8; i++)
            {
                float x = -10.5f + i * 3.0f;
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Tree_N1_{i}", i % 2 == 0 ? pineTeal : oakGreen, new Vector2(x, 4.4f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Tree_N2_{i}", i % 2 == 0 ? spruceTall ?? pineDark : pineDark, new Vector2(x + 1.4f, 6.2f));
            }
            foreach (float y in new[] { 2.8f, 0.2f, -2.6f, -5.0f })
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Tree_W_{y:F0}", pineTeal, new Vector2(-9.6f, y));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Tree_E_{y:F0}", y > 0 ? oakGold : pineDark, new Vector2(9.2f, y));
            }
            // Southern deep backdrop trees pushed below y=-8.2 (leave SE x=2..7 open so ForestPond shines).
            foreach (float x in new[] { -7.5f, -4.5f, -1.5f })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Tree_S_{x:F0}", x < -3f ? pineTeal : oakGreen, new Vector2(x, -8.5f));

            // Ranger camp decor around ForestOutpost (-4.5, -2.0) & RestSpot (-5.0, -1.5), clear of Carrier (-6,-1).
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Camp_Lantern", lampPost, new Vector2(-7.0f, -0.7f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Camp_Sack", sack, new Vector2(-2.9f, -2.25f));

            // BriarGate woodland stone/bramble framing on east trailhead (6.0, 0.5).
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Briar_Tree_Back", pineDark, new Vector2(6.1f, 2.2f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Briar_Boulder_N", boulder, new Vector2(4.9f, 1.3f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Briar_Bush_N", bushMed, new Vector2(7.1f, 1.3f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Briar_Bush_S", bushMed, new Vector2(7.0f, -1.8f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Pond_Boulder", boulder, new Vector2(2.6f, -3.5f));

            // Trailside bushes, ferns & wildflowers (clear of central path y in [-1,1]).
            foreach (var pos in new[] { new Vector2(-7.8f, 2.5f), new Vector2(-3.2f, 2.9f), new Vector2(1.4f, 2.8f), new Vector2(-0.8f, -3.1f), new Vector2(2.6f, -2.8f) })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Wild_Bush_{pos.x:F0}_{pos.y:F0}", bushMed, pos);

            foreach (var pos in new[] { new Vector2(-2.5f, 2.0f), new Vector2(2.0f, 2.1f), new Vector2(-1.6f, -2.2f), new Vector2(4.2f, -2.3f) })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Wild_Flower_{pos.x:F0}_{pos.y:F0}", flower, pos);

            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Wild_Fern_1", fern, new Vector2(-7.0f, -2.9f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Wild_Boulder_NW", boulder, new Vector2(-4.4f, 3.2f));
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
                    scene, new Vector2(-8f, -4.5f), new Vector2(8f, 4.8f),
                    5.2f, new Color(0.06f, 0.11f, 0.06f, 1f));

                var playerProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Player", "Assets/Art/Characters/Player/Body_A/Idle_Base/Idle_Down-Sheet.png",
                    "Idle_Down-Sheet_0", "Assets/Animations/Player/Player_BodyA.controller",
                    new Vector2(0f, -0.35f));
                var kaelenProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Kaelen", "Assets/Art/Characters/NPCs/Rogue/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_Rogue.controller",
                    new Vector2(0f, -0.35f));
                var vaneProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Vane", "Assets/Art/Characters/NPCs/Wizard/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_Wizard.controller",
                    new Vector2(0f, -0.35f), flipX: true);
                var carrierProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Eliza", "Assets/Art/Characters/NPCs/Citizen_F/Peasant_A/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_PeasantA.controller",
                    new Vector2(0f, -0.35f));

                PixelCrawlerHubToolkit.BindRoot(scene, "Player", playerProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "Kaelen", kaelenProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "Vane", vaneProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "Carrier", carrierProf);

                var restSpot = PixelCrawlerHubToolkit.FindRoot(scene, "RestSpot");
                if (restSpot != null) restSpot.transform.position = new Vector3(-4.8f, -1.7f, 0f);
                var outpost = PixelCrawlerHubToolkit.FindRoot(scene, "ForestOutpost");
                if (outpost != null) outpost.transform.position = new Vector3(-6.8f, -2.0f, 0f);

                PixelCrawlerHubToolkit.BindStaticProp(scene, "RestSpot", "Wild_Campfire", BonfirePath, "Bonfire_00");
                PixelCrawlerHubToolkit.BindStaticProp(scene, "ForestOutpost", "Wild_OutpostCrates", FurniturePath, "Furniture_13");
                PixelCrawlerHubToolkit.BindStaticProp(scene, "BlightBark", "Wild_BlightStump", TreeM1Path, "Tree_Model_01_Size_03_04");
                PixelCrawlerHubToolkit.BindStaticProp(scene, "BriarGate", "Wild_BriarGate", DungeonPropsPath, "Dungeon_Props_02");

                PixelCrawlerHubToolkit.HideTriggerVisual(scene, "TrailExit");
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
