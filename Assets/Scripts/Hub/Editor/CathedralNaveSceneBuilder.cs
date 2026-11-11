using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Rebuilds Hub_CathedralNave and its art overlay Hub_CathedralNave_Art using
    /// Pixel Crawler Dungeon_Tiles slate flagstones, stained-glass light shafts,
    /// red-gold nave carpet, stone pews, gothic banners, and animated NPCs.
    /// </summary>
    public static class CathedralNaveSceneBuilder
    {
        const string MainScenePath = "Assets/Scenes/Hub_CathedralNave.unity";
        const string ArtScenePath = "Assets/Scenes/Hub_CathedralNave_Art.unity";

        const string DungeonSheet = "Dungeon_Tiles";
        const string IntPropsPath = "Assets/Art/Environments/Buildings/Interior/Interior_Props_01.png";
        const string ExtPropsPath = "Assets/Art/Environments/Buildings/Exterior/Props.png";
        const string DungeonPropsPath = "Assets/Art/Environments/Props/Static/Dungeon_Props.png";
        const string EsotericPath = "Assets/Art/Environments/Props/Static/Esoteric.png";

        [MenuItem("Morsoul/Scenes/Cathedral Nave", priority = 16)]
        public static void RebuildMenu() => Rebuild();

        public static void Rebuild()
        {
            PixelCrawlerHubToolkit.ClearCache();
            RebuildArtOverlay();
            RebuildMainSceneBindings();
            AssetDatabase.SaveAssets();
            Debug.Log("CathedralNaveSceneBuilder: rebuilt Hub_CathedralNave and Hub_CathedralNave_Art (Pixel Crawler).");
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

                BuildNaveTilemaps(scene);
                BuildNaveProps(scene);
                PixelCrawlerHubToolkit.RebuildBounds(scene, "CathedralBounds",
                    ("Bound_North", new Vector2(0f, 4.3f), new Vector2(18f, 1.4f)),
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

        static void BuildNaveTilemaps(Scene scene)
        {
            var grid = PixelCrawlerHubToolkit.EnsureGrid(scene, "EnvironmentGrid");
            var floorMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "NaveFloor", -10, 2.0f);
            var glowMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "StainedGlassGlow", -9, 1.99f);
            var carpetMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "NaveCarpet", -8, 1.98f);
            var wallsMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "NaveWalls", -6, 1.95f);

            // Slate cathedral flagstones across entire nave.
            PixelCrawlerHubToolkit.FillRandomStaticBlock(floorMap, DungeonSheet, -9, -5, 9, 4, 0, 2, 4, 7);

            // Center crimson-and-gold ceremonial runner straight up to ValeriusAltar (world x=0.5).
            for (int y = -4; y <= 2; y++)
            {
                int tileRow = y == -4 ? 12 : (Mathf.Abs(y) % 2 == 0 ? 10 : 11);
                var runner = PixelCrawlerHubToolkit.LoadStaticTile(DungeonSheet, $"{DungeonSheet}_{tileRow}_5");
                if (runner != null) carpetMap.SetTile(new Vector3Int(0, y, 0), runner);
            }

            // Northern Gothic colonnade walls (y=4..6, plain dark slate ashlar cols 1..2).
            for (int x = -8; x <= 8; x++)
            {
                for (int dy = 0; dy < 3; dy++)
                {
                    int r = 2 - dy;
                    int c = 1 + (Mathf.Abs(x) % 2);
                    var wt = PixelCrawlerHubToolkit.LoadStaticTile(DungeonSheet, $"{DungeonSheet}_{r}_{c}");
                    if (wt != null) wallsMap.SetTile(new Vector3Int(x, 4 + dy, 0), wt);
                }
            }

            // Three glowing stained-glass windows + floor pools (Red at x=-4, Blue at x=0, Green at x=4).
            StampStainedGlassBay(wallsMap, glowMap, centerX: -4, colBase: 0);
            StampStainedGlassBay(wallsMap, glowMap, centerX: 0, colBase: 3);
            StampStainedGlassBay(wallsMap, glowMap, centerX: 4, colBase: 6);

            floorMap.RefreshAllTiles();
            glowMap.RefreshAllTiles();
            carpetMap.RefreshAllTiles();
            wallsMap.RefreshAllTiles();
        }

        static void StampStainedGlassBay(Tilemap wallsMap, Tilemap glowMap, int centerX, int colBase)
        {
            // Window grille sits on wall (r=17..18, colBase+1), colored floor reflection at y=1..3 (r=19..21).
            for (int dy = 0; dy < 2; dy++)
            {
                int wr = 18 - dy;
                var win = PixelCrawlerHubToolkit.LoadStaticTile(DungeonSheet, $"{DungeonSheet}_{wr}_{colBase + 1}");
                if (win != null) wallsMap.SetTile(new Vector3Int(centerX, 4 + dy, 0), win);
            }
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = 0; dy < 3; dy++)
                {
                    int gr = 21 - dy;
                    var g = PixelCrawlerHubToolkit.LoadStaticTile(DungeonSheet, $"{DungeonSheet}_{gr}_{colBase + dx + 1}");
                    if (g != null) glowMap.SetTile(new Vector3Int(centerX + dx, 1 + dy, 0), g);
                }
            }
        }

        static void BuildNaveProps(Scene scene)
        {
            var root = PixelCrawlerHubToolkit.EnsurePropsRoot(scene, "EnvironmentProps").transform;

            var stonePew = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_09");
            var oakBench = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Int_Interior_Props_01_99");
            var bannerRed = PixelCrawlerHubToolkit.LoadSprite(DungeonPropsPath, "Dungeon_Props_14");
            var bannerBlue = PixelCrawlerHubToolkit.LoadSprite(DungeonPropsPath, "Dungeon_Props_15");

            // Nave Pews flanking the red carpet runner (West & East), clear of Sylvan (-4,3) & Accord (4.5,1).
            foreach (float x in new[] { -1.7f, 2.7f })
            {
                foreach (float y in new[] { -1.0f, 0.7f })
                {
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Nave_Pew_{x:F0}_{y:F1}",
                        stonePew ?? oakBench, new Vector2(x, y),
                        colliderSize: new Vector2(1.7f, 0.55f), colliderOffset: new Vector2(0f, 0.25f));
                }
            }

            // Gothic Choir Banners hung along the north stone wall between windows.
            if (bannerRed != null && bannerBlue != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_Banner_W", bannerBlue, new Vector2(-6.0f, 4.6f), zBias: 0f, sortingOrder: -2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_Banner_NW", bannerRed, new Vector2(-2.0f, 4.6f), zBias: 0f, sortingOrder: -2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_Banner_NE", bannerRed, new Vector2(2.0f, 4.6f), zBias: 0f, sortingOrder: -2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_Banner_E", bannerBlue, new Vector2(6.0f, 4.6f), zBias: 0f, sortingOrder: -2);
            }
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
                    5.2f, new Color(0.06f, 0.06f, 0.09f, 1f));

                var playerProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Player", "Assets/Art/Characters/Player/Body_A/Idle_Base/Idle_Down-Sheet.png",
                    "Idle_Down-Sheet_0", "Assets/Animations/Player/Player_BodyA.controller",
                    new Vector2(0f, -0.35f));
                var sylvanProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Sylvan", "Assets/Art/Characters/NPCs/Wizard/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_Wizard.controller",
                    new Vector2(0f, -0.35f));
                var accordProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Accord", "Assets/Art/Characters/NPCs/Knight/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_Knight.controller",
                    new Vector2(0f, -0.35f), flipX: true);
                var crowdProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Eliza", "Assets/Art/Characters/NPCs/Citizen_F/Peasant_A/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_PeasantA.controller",
                    new Vector2(0f, -0.35f));
                var carrierProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Carrier", "Assets/Art/Characters/NPCs/Rogue/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_Rogue.controller",
                    new Vector2(0f, -0.35f));

                PixelCrawlerHubToolkit.BindRoot(scene, "Player", playerProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "Sylvan", sylvanProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "Accord", accordProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "NaveCrowd", crowdProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "Carrier", carrierProf);

                var altar = PixelCrawlerHubToolkit.FindRoot(scene, "ValeriusAltar");
                if (altar != null) altar.transform.position = new Vector3(0.5f, 3.0f, 0f);

                PixelCrawlerHubToolkit.BindStaticProp(scene, "ValeriusAltar", "Cath_Altar", IntPropsPath, "Int_Interior_Props_01_05");
                PixelCrawlerHubToolkit.BindStaticProp(scene, "TuningFork", "Cath_Tuning", EsotericPath, "Esoteric_41");
                PixelCrawlerHubToolkit.BindStaticProp(scene, "RestSpot", "Cath_RestBench", ExtPropsPath, "Ext_Props_09");
                PixelCrawlerHubToolkit.BindStaticProp(scene, "BelfryStair", "Cath_BelfryDoor", DungeonPropsPath, "Dungeon_Props_02");

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

