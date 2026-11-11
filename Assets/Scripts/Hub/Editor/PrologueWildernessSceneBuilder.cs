using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Builds the dedicated 2D wilderness approach between the ruins gate and
    /// the Lower Market using Pixel Crawler Tilemaps, RuleTiles, and Y-sorted
    /// environment sprites. Preserves all Prologue cutscene staging and colliders.
    /// </summary>
    public static class PrologueWildernessSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Prologue_Wilderness.unity";
        const string SceneName = "Prologue_Wilderness";

        const string TreeM1Path = "Assets/Art/Environments/Props/Trees/Model_01/Size_03.png";
        const string TreeM2Path = "Assets/Art/Environments/Props/Trees/Model_02/Size_03.png";
        const string TreeM3Path = "Assets/Art/Environments/Props/Trees/Model_03/Size_03.png";
        const string VegPath = "Assets/Art/Environments/Props/Static/Vegetation.png";
        const string RocksPath = "Assets/Art/Environments/Props/Static/Rocks.png";
        const string PlayerSheetPath = "Assets/Art/Characters/Player/Body_A/Idle_Base/Idle_Down-Sheet.png";
        const string ElizaSheetPath = "Assets/Art/Characters/NPCs/Citizen_F/Peasant_A/Idle/Idle-Sheet.png";
        const string PlayerControllerPath = "Assets/Animations/Player/Player_BodyA.controller";
        const string ElizaControllerPath = "Assets/Animations/NPCs/NPC_CitizenF_PeasantA.controller";
        const string EyeGlintPath = "Assets/Art/Vfx/BushEyeGlint.png";

        [MenuItem("Morsoul/Hub/Create Prologue Wilderness Scene")]
        public static void CreateOrUpdate() => BuildInternal(prompt: true);

        public static void ForceUpdate() => BuildInternal(prompt: false);

        static void BuildInternal(bool prompt)
        {
            var preExisting = SceneManager.GetSceneByPath(ScenePath);
            bool wasOpen = preExisting.IsValid() && preExisting.isLoaded;
            if (wasOpen && preExisting.isDirty)
            {
                Debug.LogError($"PrologueWildernessSceneBuilder: '{ScenePath}' is open " +
                               "with unsaved changes; skipped. Save or revert it first.");
                return;
            }

            bool created = !File.Exists(ScenePath);
            if (!created && prompt && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Prologue Wilderness Scene",
                    $"'{ScenePath}' already exists. Rebuild it with Pixel Crawler tilemaps & sprites?",
                    "Update", "Cancel"))
            {
                Debug.LogWarning($"PrologueWildernessSceneBuilder: '{ScenePath}' left untouched.");
                return;
            }

            PixelCrawlerHubToolkit.ClearCache();
            var previousActive = SceneManager.GetActiveScene();
            var scene = wasOpen ? preExisting
                : created ? HubSceneFactory.NewEmptyScene()
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var context = EnsureBaseScene(scene);
                ConfigurePlayerAndCamera(scene, context);

                // Clean up legacy Quad primitives if upgrading from blockout.
                RemoveLegacyQuadVisuals(scene);

                BuildTilemapTerrain(scene);
                BuildEnvironmentProps(scene);
                BuildPlayableCorridorColliders(scene);

                var eliza = BuildElizaActor(scene);
                var elizaSurvey = EnsureEmpty("ElizaSurveySpot", new Vector3(14.1f, 0.4f, 0f));
                var bush = BuildElizaCoverBush(scene);
                var beacon = BuildEyeHighlight(scene);
                var blocker = BuildRouteBlocker(scene);

                var player = FindComponent<HubPlayerController>(scene);
                var camera = FindComponent<CameraFollow2D>(scene);
                WireEmotesAndTrigger(scene, context, player, camera, eliza, elizaSurvey, bush, beacon, blocker);

                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene, ScenePath))
                    Debug.Log($"PrologueWildernessSceneBuilder: '{ScenePath}' {(created ? "created" : "updated")} (Pixel Crawler).");
                else
                    Debug.LogError($"PrologueWildernessSceneBuilder: failed to save '{ScenePath}'.");
                RegisterBuildScene();
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
                if (!wasOpen && scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void ConfigurePlayerAndCamera(Scene scene, HubContext context)
        {
            var player = FindComponent<HubPlayerController>(scene);
            var camera = FindComponent<CameraFollow2D>(scene);

            if (player != null)
            {
                var playerProfile = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Player", PlayerSheetPath, "Idle_Down-Sheet_0",
                    PlayerControllerPath, new Vector2(0f, -0.35f), flipX: false);
                PixelCrawlerHubToolkit.BindActorVisual(player.gameObject, playerProfile, addYSort: true);
            }

            if (camera != null)
            {
                camera.useBounds = true;
                camera.boundsMin = new Vector2(-11f, -3.5f);
                camera.boundsMax = new Vector2(25f, 3.5f);
                if (player != null)
                {
                    camera.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, -10f);
                    camera.SnapToTarget(player.transform);
                }
                var cam = camera.GetComponent<Camera>();
                if (cam != null)
                {
                    cam.orthographic = true;
                    cam.orthographicSize = 5f;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.08f, 0.13f, 0.08f, 1f);
                }
            }

            var hud = FindComponent<HubHudController>(scene);
            if (hud == null)
            {
                var hudGo = new GameObject("HubHudPanel");
                var renderer = hudGo.AddComponent<UnityEngine.UIElements.PanelRenderer>();
                var panelSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/UI Toolkit/MainPanelSettings.asset");
                if (panelSettings != null) renderer.panelSettings = panelSettings;
                var uxml = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>("Assets/HubHud.uxml");
                if (uxml != null) renderer.visualTreeAsset = uxml;
                hud = hudGo.AddComponent<HubHudController>();
            }
            hud.caseText = "Prologue";
            hud.objectiveText = "Investigate the wilderness path toward the settlement.";
            if (context != null) context.hud = hud;
        }

        static void RemoveLegacyQuadVisuals(Scene scene)
        {
            foreach (var name in new[]
            {
                "Environment", "WildernessPath", "ElizaIntroCameraTarget",
                "ElizaCutawayCameraTarget", "ElizaWalkStart", "AncientChamberKey", "DiagnosticBeacon"
            })
            {
                RemoveRoot(scene, name);
            }
        }

        static void BuildTilemapTerrain(Scene scene)
        {
            var grid = PixelCrawlerHubToolkit.EnsureGrid(scene, "EnvironmentGrid");
            var dirtTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Dirt");
            var grassTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Grass");
            var cobbleTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Cobble");
            var waterTile = PixelCrawlerHubToolkit.LoadRuleTile("RT_Water");

            var dirtMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "GroundDirt", sortingOrder: -10, zOffset: 2.0f);
            var cobbleMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "GroundCobble", sortingOrder: -9, zOffset: 1.99f);
            var grassMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "GroundGrass", sortingOrder: -8, zOffset: 1.98f);
            var waterMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "WaterPond", sortingOrder: -7, zOffset: 1.94f, addCollider: true);

            // Base dirt underlay across full viewport bounds.
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -20, -10, 36, 10);

            // Ruin stones threshold at the west ruins gate (underneath grass overhang).
            PixelCrawlerHubToolkit.FillRect(cobbleMap, cobbleTile, -16, -4, -8, 3);

            // Grass blanket with carved dirt trail.
            PixelCrawlerHubToolkit.FillRect(grassMap, grassTile, -20, -10, 36, 10);

            // Organic winding dirt road through the forest (2-3 tiles wide).
            PixelCrawlerHubToolkit.ClearRect(grassMap, -15, -2, -7, 1);  // Ruins courtyard
            PixelCrawlerHubToolkit.ClearRect(grassMap, -7, -1, 0, 0);    // West approach
            PixelCrawlerHubToolkit.ClearRect(grassMap, 0, -2, 6, 0);     // Mid bend south
            PixelCrawlerHubToolkit.ClearRect(grassMap, 6, -1, 10, 1);    // Rise toward glade
            PixelCrawlerHubToolkit.ClearRect(grassMap, 10, -2, 16, 1);   // Eliza ambush glade
            PixelCrawlerHubToolkit.ClearRect(grassMap, 16, -1, 21, 0);   // East neck
            PixelCrawlerHubToolkit.ClearRect(grassMap, 21, -2, 34, 1);   // Road to Lower Market

            // Forest streams/ponds nestled safely off the trail edges.
            PixelCrawlerHubToolkit.FillRect(waterMap, waterTile, -6, 5, 2, 8);
            PixelCrawlerHubToolkit.FillRect(waterMap, waterTile, 16, -8, 23, -5);

            dirtMap.RefreshAllTiles();
            grassMap.RefreshAllTiles();
            cobbleMap.RefreshAllTiles();
            waterMap.RefreshAllTiles();
        }

        static void BuildEnvironmentProps(Scene scene)
        {
            var root = PixelCrawlerHubToolkit.EnsurePropsRoot(scene, "EnvironmentProps");

            var oakGreen = PixelCrawlerHubToolkit.LoadSprite(TreeM1Path, "Tree_Model_01_Size_03_00");
            var oakGold = PixelCrawlerHubToolkit.LoadSprite(TreeM1Path, "Tree_Model_01_Size_03_01");
            var oakDead = PixelCrawlerHubToolkit.LoadSprite(TreeM1Path, "Tree_Model_01_Size_03_02");
            var oakOrange = PixelCrawlerHubToolkit.LoadSprite(TreeM1Path, "Tree_Model_01_Size_03_07");
            var stump = PixelCrawlerHubToolkit.LoadSprite(TreeM1Path, "Tree_Model_01_Size_03_04");

            var pineA = PixelCrawlerHubToolkit.LoadSprite(TreeM2Path, "Tree_Model_02_Size_03_00");
            var pineB = PixelCrawlerHubToolkit.LoadSprite(TreeM2Path, "Tree_Model_02_Size_03_01");
            var tallPine = PixelCrawlerHubToolkit.LoadSprite(TreeM3Path, "Tree_Model_03_Size_03_00");

            var bushMed = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_04");
            var bushSmall = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_12");
            var fern = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_26");
            var flowerA = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_34");
            var flowerB = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_35");
            var deadShrub = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_95");

            var greyPillar = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_04");
            var greyBoulder = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_05");
            var brownBoulder = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_01");
            var smallRock = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_03");

            // Ruins gate framing (West).
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Ruin_Monolith_N", greyPillar, new Vector2(-10.2f, 1.4f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Ruin_Monolith_S", greyPillar, new Vector2(-10.2f, -2.8f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Ruin_Boulder_NW", greyBoulder, new Vector2(-8.7f, 1.9f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Ruin_Boulder_SW", greyBoulder, new Vector2(-8.6f, -2.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Ruin_DeadShrub", deadShrub, new Vector2(-7.8f, 2.2f));

            // North forest treeline (staggered deep backdrop + border row).
            Sprite[] northPool = { oakGreen, pineA, oakGold, pineB, oakOrange, tallPine };
            for (int i = 0; i < 16; i++)
            {
                float x = -12f + i * 2.5f;
                // Skip water creek gap between x=-6..2 on the back row so pond shows cleanly.
                if (x < -6.5f || x > 2.5f)
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Tree_N_Back_{i:D2}", northPool[(i + 2) % northPool.Length], new Vector2(x + 1.1f, 4.7f));
                var tree = i == 6 ? oakDead : northPool[i % northPool.Length];
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Tree_N_{i:D2}", tree, new Vector2(x, 2.55f + (i % 2) * 0.45f));
            }

            // South forest border: keep canopy tops below the walking path so actors stay unoccluded.
            Sprite[] southCurbProps = { bushMed, stump, bushSmall, greyBoulder, bushMed, brownBoulder };
            Sprite[] southDeepTrees = { pineB, pineA, oakGreen, oakGold, pineA };
            for (int i = 0; i < 16; i++)
            {
                float x = -11.5f + i * 2.45f;
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Curb_S_{i:D2}", southCurbProps[i % southCurbProps.Length], new Vector2(x, -3.55f - (i % 2) * 0.35f));
                if (x < 15.5f || x > 23.5f)
                {
                    var tree = southDeepTrees[i % southDeepTrees.Length];
                    float yPos = tree == pineA || tree == pineB ? -7.0f : -7.9f;
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Tree_S_Back_{i:D2}", tree, new Vector2(x + 0.8f, yPos));
                }
            }

            // Trailside bushes, stumps, rocks & wildflowers (staying clear of walking lane y[-1.6, 1.6]).
            var bushes = new (Vector2 pos, Sprite spr)[]
            {
                (new Vector2(-5.4f, 1.9f), bushMed),
                (new Vector2(2.8f, 1.85f), bushSmall),
                (new Vector2(8.5f, 1.95f), bushMed),
                (new Vector2(18.2f, 1.85f), bushMed),
                (new Vector2(-3.2f, -2.35f), bushMed),
                (new Vector2(6.5f, -2.45f), bushSmall),
                (new Vector2(14.8f, -2.4f), bushMed),
            };
            for (int i = 0; i < bushes.Length; i++)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Veg_Bush_{i:D2}", bushes[i].spr, bushes[i].pos);

            foreach (var x in new[] { -6.5f, 4.5f, 11.2f, 20.5f })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Stump_{x:F0}", stump, new Vector2(x, 2.1f));

            var scatter = new (Vector2 pos, Sprite spr)[]
            {
                (new Vector2(-4.1f, -2.0f), fern),
                (new Vector2(-1.0f, 1.75f), flowerA),
                (new Vector2(0.8f, -2.15f), fern),
                (new Vector2(4.2f, -2.2f), flowerB),
                (new Vector2(7.6f, 1.8f), flowerA),
                (new Vector2(9.4f, -2.1f), brownBoulder),
                (new Vector2(11.6f, -2.05f), flowerB),
                (new Vector2(15.2f, 1.8f), fern),
                (new Vector2(16.7f, -2.35f), smallRock),
                (new Vector2(21.4f, 1.7f), flowerA),
            };
            for (int i = 0; i < scatter.Length; i++)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Veg_Detail_{i:D2}", scatter[i].spr, scatter[i].pos);
        }

        static void BuildPlayableCorridorColliders(Scene scene)
        {
            EnsureBoundary("NorthTreeLine", new Vector3(8f, 2.65f, 0f), new Vector2(42f, 1.2f));
            EnsureBoundary("SouthTreeLine", new Vector3(8f, -3.1f, 0f), new Vector2(42f, 1.4f));
            EnsureBoundary("WestBoundary", new Vector3(-10f, -0.5f, 0f), new Vector2(1f, 7f));
        }

        static GameObject BuildElizaActor(Scene scene)
        {
            var existing = PixelCrawlerHubToolkit.FindRoot(scene, "Eliza");
            if (existing != null) Object.DestroyImmediate(existing);

            var eliza = new GameObject("Eliza");
            SceneManager.MoveGameObjectToScene(eliza, scene);
            eliza.transform.position = new Vector3(12f, 0f, 0f);

            var elizaProfile = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                "Eliza", ElizaSheetPath, "Idle-Sheet_0",
                ElizaControllerPath, new Vector2(0f, -0.35f), flipX: true);
            PixelCrawlerHubToolkit.BindActorVisual(eliza, elizaProfile, addYSort: true);
            eliza.SetActive(false);
            return eliza;
        }

        static GameObject BuildElizaCoverBush(Scene scene)
        {
            var existing = PixelCrawlerHubToolkit.FindRoot(scene, "ElizaCover");
            if (existing != null) Object.DestroyImmediate(existing);

            var go = new GameObject("ElizaCover");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(12.7f, -0.15f, -0.15f * YSort2D.DepthPerUnitY - 0.005f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_00");
            go.AddComponent<YSort2D>().bias = -0.005f;
            return go;
        }

        static GameObject BuildEyeHighlight(Scene scene)
        {
            var existing = PixelCrawlerHubToolkit.FindRoot(scene, "EyeHighlight");
            if (existing != null) Object.DestroyImmediate(existing);

            EnsureEyeGlintSpriteAsset();
            var go = new GameObject("EyeHighlight");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(12.7f, 0.65f, -0.04f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EyeGlintPath);
            sr.color = new Color(0.25f, 0.95f, 1f, 0.95f);
            sr.sortingOrder = 6;
            go.SetActive(false);
            return go;
        }

        static GameObject BuildRouteBlocker(Scene scene)
        {
            var existing = PixelCrawlerHubToolkit.FindRoot(scene, "EasternRouteBlocker");
            if (existing != null) Object.DestroyImmediate(existing);

            var go = new GameObject("EasternRouteBlocker");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(24f, -0.5f, 0f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = false;
            col.size = new Vector2(1.2f, 4f);

            var boulder = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_00");
            var boulderSmall = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_05");
            var bramble = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_94");
            PixelCrawlerHubToolkit.PlaceSpriteProp(go.transform, "Blocker_RockN", boulder, new Vector2(24f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(go.transform, "Blocker_Bramble", bramble, new Vector2(23.85f, -0.45f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(go.transform, "Blocker_RockS", boulderSmall, new Vector2(24f, -1.6f));
            return go;
        }

        static void WireEmotesAndTrigger(
            Scene scene, HubContext context, HubPlayerController player, CameraFollow2D camera,
            GameObject eliza, GameObject elizaSurvey, GameObject bush, GameObject beacon, GameObject blocker)
        {
            var elizaEmote = EnsureLabel("ElizaEmote", "", eliza.transform.position + new Vector3(0f, 1.4f, 0f));
            elizaEmote.transform.SetParent(eliza.transform, true);
            elizaEmote.gameObject.SetActive(false);

            var playerEmote = EnsureLabel("PlayerEmote", "", player.transform.position + new Vector3(0f, 1.4f, 0f));
            playerEmote.transform.SetParent(player.transform, true);
            playerEmote.gameObject.SetActive(false);

            var trigger = EnsureEmpty("ElizaIntroTrigger", new Vector3(4.1f, -0.5f, 0f));
            var triggerCollider = EnsureComponent<BoxCollider2D>(trigger);
            triggerCollider.isTrigger = true;
            triggerCollider.size = new Vector2(2f, 5f);

            var intro = EnsureComponent<PrologueElizaIntroController>(trigger);
            intro.context = context;
            intro.player = player;
            intro.cameraFollow = camera;
            intro.elizaSurveyMarker = elizaSurvey.transform;
            intro.eliza = eliza.transform;
            intro.elizaProfile = AssetDatabase.LoadAssetAtPath<VisualProfile>("Assets/Art/Profiles/Eliza.asset");
            intro.bushProp = bush.transform;
            intro.eyeHighlight = beacon.transform;
            intro.elizaEmote = elizaEmote;
            intro.playerEmote = playerEmote;
            intro.routeBlocker = blocker;
            intro.safeResumePosition = new Vector3(1.5f, 0f, 0f);
            intro.nextSceneName = "Hub_LowerMarket";
            intro.nextCaseId = 1;
            intro.autoLoadNextScene = true;
        }

        static void EnsureEyeGlintSpriteAsset()
        {
            if (File.Exists(EyeGlintPath)) return;
            PixelCrawlerHubToolkit.EnsureFolder("Assets/Art/Vfx");

            var tex = new Texture2D(16, 8, TextureFormat.RGBA32, false);
            var clear = new Color32(0, 0, 0, 0);
            var glow = new Color32(100, 240, 255, 160);
            var core = new Color32(220, 255, 255, 255);
            var pixels = new Color32[16 * 8];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            // Two crisp 16-bit glowing eye slits inside bush shadows.
            foreach (int cx in new[] { 4, 11 })
            {
                for (int dx = -1; dx <= 1; dx++)
                    pixels[4 * 16 + (cx + dx)] = glow;
                pixels[3 * 16 + cx] = core;
                pixels[4 * 16 + cx] = core;
                pixels[5 * 16 + cx] = glow;
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(EyeGlintPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(EyeGlintPath, ImportAssetOptions.ForceUpdate);

            var ti = AssetImporter.GetAtPath(EyeGlintPath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 16f;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
        }

        // --- Shared Scene Boilerplate ---------------------------------------

        static HubContext EnsureBaseScene(Scene scene)
        {
            var player = FindComponent<HubPlayerController>(scene);
            if (player == null)
            {
                var playerGo = HubSceneFactory.CreatePlayer();
                player = playerGo.GetComponent<HubPlayerController>();
            }
            player.transform.position = new Vector3(-8f, 0f, 0f);

            var camera = FindComponent<CameraFollow2D>(scene);
            if (camera == null) camera = HubSceneFactory.CreateCamera(player.transform);

            var spawn = FindRoot(scene, "PlayerSpawn");
            if (spawn == null)
            {
                spawn = new GameObject("PlayerSpawn");
                spawn.tag = "PlayerSpawn";
            }
            spawn.transform.position = new Vector3(-8f, 0f, 0f);

            var entry = FindComponent<SceneEntryPoint>(scene);
            if (entry == null)
            {
                var entryGo = new GameObject("SceneEntryPoint");
                entry = entryGo.AddComponent<SceneEntryPoint>();
            }
            entry.spawnPointTag = "PlayerSpawn";
            entry.player = player.transform;

            var context = FindComponent<HubContext>(scene);
            if (context == null) context = HubSceneFactory.CreateHubContext();
            HubSceneFactory.WirePlayer(player.gameObject, camera, context);
            return context;
        }

        static T FindComponent<T>(Scene scene) where T : Component
        {
            foreach (var component in Object.FindObjectsByType<T>())
                if (component != null && component.gameObject.scene == scene)
                    return component;
            return null;
        }

        static GameObject FindRoot(Scene scene, string name) =>
            PixelCrawlerHubToolkit.FindRoot(scene, name);

        static void RemoveRoot(Scene scene, string name)
        {
            var go = FindRoot(scene, name);
            if (go != null) Object.DestroyImmediate(go);
        }

        static GameObject EnsureEmpty(string name, Vector3 position)
        {
            var scene = SceneManager.GetActiveScene();
            var go = FindRoot(scene, name) ?? new GameObject(name);
            go.transform.position = position;
            return go;
        }

        static GameObject EnsureBoundary(string name, Vector3 position, Vector2 size)
        {
            var go = EnsureEmpty(name, position);
            foreach (var mr in go.GetComponents<MeshRenderer>()) Object.DestroyImmediate(mr);
            foreach (var mf in go.GetComponents<MeshFilter>()) Object.DestroyImmediate(mf);
            var collider = EnsureComponent<BoxCollider2D>(go);
            collider.isTrigger = false;
            collider.size = size;
            return go;
        }

        static InteractableLabel EnsureLabel(string name, string text, Vector3 position)
        {
            var scene = SceneManager.GetActiveScene();
            var go = FindRoot(scene, name);
            InteractableLabel label;
            if (go == null)
            {
                go = HubSceneFactory.NewWorldLabel(name, text, position,
                    HubSceneFactory.NameFontSize + 4, Color.white);
                label = go.GetComponent<InteractableLabel>();
            }
            else
            {
                go.transform.SetParent(null, true);
                go.transform.position = position;
                label = EnsureComponent<InteractableLabel>(go);
            }
            label.text = text;
            label.fontSize = HubSceneFactory.NameFontSize + 4;
            return label;
        }

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        static void RegisterBuildScene()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var scene in list)
            {
                if (scene.path == ScenePath)
                {
                    scene.enabled = true;
                    EditorBuildSettings.scenes = list.ToArray();
                    return;
                }
            }
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
