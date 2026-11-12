using Morsoul.Dialogue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Rebuilds Hub_CathedralNave and its art overlay Hub_CathedralNave_Art using
    /// Pixel Crawler Dungeon_Tiles slate flagstones, fixed stained-glass light shafts,
    /// wide crimson-gold nave carpet, stone pews, gothic banners, belfry stairs, and NPCs.
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
        const string BelfryStairsPath = "Assets/Art/Environments/Custom/Cathedral_Stairs_Up.png";
        const string CarpetRunnerPath = "Assets/Art/Environments/Custom/Cathedral_Carpet_Runner.png";
        const string VillagePropsRoot = "Assets/Art/Environments/Village_Interiors/furniture_and_props_sprites/";
        const string BarksPath = "Assets/Data/Dialogue/Case02/D_NaveBarks_02.asset";

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
                    ("Bound_North", new Vector2(0f, 5.8f), new Vector2(28f, 1.4f)),
                    ("Bound_South", new Vector2(0f, -7.2f), new Vector2(28f, 1.4f)),
                    ("Bound_West", new Vector2(-12.2f, 0f), new Vector2(1.4f, 15f)),
                    ("Bound_East", new Vector2(12.2f, 0f), new Vector2(1.4f, 15f)));

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
            var windowMap = PixelCrawlerHubToolkit.EnsureTilemapLayer(grid.transform, "StainedGlassWindows", -5, 1.94f);

            // Expanded 25x13 slate cathedral flagstones (x=-12..12, y=-6..6).
            PixelCrawlerHubToolkit.FillRandomStaticBlock(floorMap, DungeonSheet, -13, -7, 13, 6, 0, 2, 4, 7);

            // Northern Gothic colonnade walls (x=-12..12, y=6..8, slate ashlar cols 1..2).
            for (int x = -12; x <= 12; x++)
            {
                for (int dy = 0; dy < 3; dy++)
                {
                    int r = 2 - dy;
                    int c = 1 + (Mathf.Abs(x) % 2);
                    var wt = PixelCrawlerHubToolkit.LoadStaticTile(DungeonSheet, $"{DungeonSheet}_{r}_{c}");
                    if (wt != null) wallsMap.SetTile(new Vector3Int(x, 6 + dy, 0), wt);
                }
            }

            // West & East perimeter stone walls enclosing the nave (x=-13..-12 and x=12..13, y=-7..8).
            for (int y = -7; y <= 8; y++)
            {
                int r = y >= 7 ? 0 : (Mathf.Abs(y) % 2 == 0 ? 2 : 1);
                var westTile = PixelCrawlerHubToolkit.LoadStaticTile(DungeonSheet, $"{DungeonSheet}_{r}_1");
                var eastTile = PixelCrawlerHubToolkit.LoadStaticTile(DungeonSheet, $"{DungeonSheet}_{r}_2");
                if (westTile != null)
                {
                    wallsMap.SetTile(new Vector3Int(-13, y, 0), westTile);
                    wallsMap.SetTile(new Vector3Int(-12, y, 0), eastTile ?? westTile);
                }
                if (eastTile != null)
                {
                    wallsMap.SetTile(new Vector3Int(12, y, 0), westTile ?? eastTile);
                    wallsMap.SetTile(new Vector3Int(13, y, 0), eastTile);
                }
            }

            // South stone wall framing the South Cathedral Exit doorway (x=-11..-2 and x=2..11, y=-7).
            for (int x = -11; x <= 11; x++)
            {
                if (x >= -1 && x <= 1) continue; // Keep center 3-tile entrance doorway open.
                int c = 1 + (Mathf.Abs(x) % 2);
                var wt = PixelCrawlerHubToolkit.LoadStaticTile(DungeonSheet, $"{DungeonSheet}_1_{c}");
                if (wt != null) wallsMap.SetTile(new Vector3Int(x, -7, 0), wt);
            }

            // Three glowing stained-glass windows (Red at x=-6, Blue at x=0, Green at x=6) layered cleanly over wallsMap.
            StampStainedGlassBay(windowMap, glowMap, centerX: -6, colBase: 0);
            StampStainedGlassBay(windowMap, glowMap, centerX: 0, colBase: 3);
            StampStainedGlassBay(windowMap, glowMap, centerX: 6, colBase: 6);

            floorMap.RefreshAllTiles();
            glowMap.RefreshAllTiles();
            carpetMap.RefreshAllTiles();
            wallsMap.RefreshAllTiles();
            windowMap.RefreshAllTiles();
        }

        static void StampStainedGlassBay(Tilemap windowMap, Tilemap glowMap, int centerX, int colBase)
        {
            // Window arch sits on windowMap (y=6, r=18) over the solid stone wall, casting colored light at y=3..5.
            var win = PixelCrawlerHubToolkit.LoadStaticTile(DungeonSheet, $"{DungeonSheet}_18_{colBase + 1}");
            if (win != null) windowMap.SetTile(new Vector3Int(centerX, 6, 0), win);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = 0; dy < 3; dy++)
                {
                    int gr = 21 - dy;
                    var g = PixelCrawlerHubToolkit.LoadStaticTile(DungeonSheet, $"{DungeonSheet}_{gr}_{colBase + dx + 1}");
                    if (g != null) glowMap.SetTile(new Vector3Int(centerX + dx, 3 + dy, 0), g);
                }
            }
        }

        static void BuildNaveProps(Scene scene)
        {
            var root = PixelCrawlerHubToolkit.EnsurePropsRoot(scene, "EnvironmentProps").transform;

            var carpetRunner = EnsureSingleSprite(CarpetRunnerPath, 16f);
            if (carpetRunner != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_Carpet_Runner", carpetRunner,
                    new Vector2(0.5f, -1.0f), zBias: 0f, sortingOrder: -8);

            var stonePew = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_09");
            var oakBench = PixelCrawlerHubToolkit.LoadSprite(IntPropsPath, "Int_Interior_Props_01_99");
            var bannerRed = PixelCrawlerHubToolkit.LoadSprite(DungeonPropsPath, "Dungeon_Props_14");
            var bannerBlue = PixelCrawlerHubToolkit.LoadSprite(DungeonPropsPath, "Dungeon_Props_15");
            var archiveCabinet = AssetDatabase.LoadAssetAtPath<Sprite>(VillagePropsRoot + "cabinet_20.png");
            var almsChest = AssetDatabase.LoadAssetAtPath<Sprite>(VillagePropsRoot + "chests_1.png");
            var sideTable = AssetDatabase.LoadAssetAtPath<Sprite>(VillagePropsRoot + "cabinet_21.png");
            var plantBroad = AssetDatabase.LoadAssetAtPath<Sprite>(VillagePropsRoot + "plants_1.png");
            var plantLush = AssetDatabase.LoadAssetAtPath<Sprite>(VillagePropsRoot + "plants_17.png");

            // 8 Nave Pews (2 aisles x 4 rows) flanking the crimson ceremonial carpet.
            foreach (float x in new[] { -3.5f, 4.5f })
            {
                foreach (float y in new[] { -3.8f, -1.8f, 0.2f, 2.2f })
                {
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Nave_Pew_{x:F0}_{y:F1}",
                        stonePew ?? oakBench, new Vector2(x, y),
                        colliderSize: new Vector2(1.7f, 0.55f), colliderOffset: new Vector2(0f, 0.25f));
                }
            }

            // Choir & Nave Banners hung along the north stone wall between the stained-glass bays.
            if (bannerRed != null && bannerBlue != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_Banner_WW", bannerBlue, new Vector2(-9.0f, 6.6f), zBias: 0f, sortingOrder: -2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_Banner_NW", bannerRed, new Vector2(-3.0f, 6.6f), zBias: 0f, sortingOrder: -2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_Banner_NE", bannerRed, new Vector2(3.0f, 6.6f), zBias: 0f, sortingOrder: -2);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_Banner_EE", bannerBlue, new Vector2(9.0f, 6.6f), zBias: 0f, sortingOrder: -2);
            }

            // West Prayer/Archive Pedestal & East Alms Chest.
            if (sideTable != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_Archive_W", sideTable, new Vector2(-7.8f, 2.0f),
                    colliderSize: new Vector2(1.8f, 0.75f), colliderOffset: new Vector2(0f, 0.45f));
            if (almsChest != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_AlmsChest_E", almsChest, new Vector2(10.0f, -1.4f),
                    colliderSize: new Vector2(1.5f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));
            if (archiveCabinet != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_Archive_NW", archiveCabinet, new Vector2(-10.2f, 5.2f),
                    colliderSize: new Vector2(1.8f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));
            if (plantBroad != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_Plant_SW", plantBroad, new Vector2(-2.3f, -5.8f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_Plant_ChancelW", plantBroad, new Vector2(-2.0f, 4.4f));
            }
            if (plantLush != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_Plant_SE", plantLush, new Vector2(3.3f, -5.8f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Nave_Plant_ChancelE", plantLush, new Vector2(3.0f, 4.4f));
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
                    scene, new Vector2(-12.5f, -7.2f), new Vector2(12.5f, 7.2f),
                    5.4f, new Color(0.06f, 0.06f, 0.09f, 1f));

                var playerProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Player", "Assets/Art/Characters/Player/Golem/Idle/Idle_Down-Sheet.png",
                    "Idle_Down-Sheet_0", "Assets/Animations/Player/Player_Golem.controller",
                    new Vector2(0f, -0.35f));
                var sylvanProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Sylvan", "Assets/Art/Characters/NPCs/Citizen_M/Villager_A/Idle/Idle_Side-Sheet.png",
                    "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_VillagerA.controller",
                    new Vector2(0f, -0.35f));
                var accordProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Accord", "Assets/Art/Characters/NPCs/Named/Accord/Idle/Idle_Down-Sheet.png",
                    "Idle_Down-Sheet_0", "Assets/Animations/NPCs/NPC_Accord.controller",
                    new Vector2(0f, -0.35f), flipX: true);
                var crowdProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "PeasantF", "Assets/Art/Characters/NPCs/Citizen_F/Peasant_A/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_PeasantA.controller",
                    new Vector2(0f, -0.35f));
                var guardProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "NaveGuard", "Assets/Art/Characters/NPCs/Citizen_M/Guard_A/Idle/Idle_Side-Sheet.png",
                    "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_GuardA.controller",
                    new Vector2(0f, -0.35f));

                // Carrier drops the player off outside in Hub_CathedralCourtyard; no RestSpot inside the Nave.
                var indoorCarrier = PixelCrawlerHubToolkit.FindRoot(scene, "Carrier");
                if (indoorCarrier != null) Object.DestroyImmediate(indoorCarrier);
                var restSpot = PixelCrawlerHubToolkit.FindRoot(scene, "RestSpot");
                if (restSpot != null) Object.DestroyImmediate(restSpot);

                // Spawn inside the South Cathedral entrance when arriving from Hub_CathedralCourtyard.
                var spawn = GameObject.FindGameObjectWithTag("PlayerSpawn");
                if (spawn != null) spawn.transform.position = new Vector3(0.5f, -4.8f, 0f);
                var entryMarker = PixelCrawlerHubToolkit.FindRoot(scene, "Spawn_CathedralDoor");
                if (entryMarker == null)
                {
                    entryMarker = new GameObject("Spawn_CathedralDoor");
                    entryMarker.AddComponent<SceneSpawnMarker>().entryId = "cathedral_door";
                }
                entryMarker.transform.position = new Vector3(0.5f, -4.8f, 0f);

                // South Cathedral Exit Doorway returning to Hub_CathedralCourtyard (flush in South wall gap).
                var exitGo = PixelCrawlerHubToolkit.FindRoot(scene, "CathedralExit");
                GateInteractable exitGate = exitGo != null ? exitGo.GetComponent<GateInteractable>() : null;
                if (exitGate == null)
                {
                    HubSceneFactory.CreateInteractable("CathedralExit",
                        new Color(0.55f, 0.4f, 0.25f), new Vector3(0.5f, -6.7f, 0f), out exitGate);
                }
                else
                {
                    exitGate.transform.position = new Vector3(0.5f, -6.7f, 0f);
                }
                exitGate.promptText = "[E] Exit Courtyard";
                exitGate.requiredFlag = "";
                exitGate.targetScene = "Hub_CathedralCourtyard";
                exitGate.entryId = "courtyard_door";
                PixelCrawlerHubToolkit.HideTriggerVisual(scene, "CathedralExit");

                // Reposition key characters and interactables into the grander Nave layout.
                SetPos(scene, "Player", new Vector3(0.5f, -4.8f, 0f));
                SetPos(scene, "Sylvan", new Vector3(-6.0f, 3.2f, 0f));
                SetPos(scene, "Accord", new Vector3(6.5f, 2.0f, 0f));
                SetPos(scene, "NaveCrowd", new Vector3(-2.0f, -3.0f, 0f));
                SetPos(scene, "ValeriusAltar", new Vector3(0.5f, 3.8f, 0f));
                SetPos(scene, "TuningFork", new Vector3(-7.8f, 2.95f, 0f));
                SetPos(scene, "BelfryStair", new Vector3(9.8f, 4.2f, 0f));

                PixelCrawlerHubToolkit.BindRoot(scene, "Player", playerProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "Sylvan", sylvanProf);
                var sylvanGo = PixelCrawlerHubToolkit.FindRoot(scene, "Sylvan");
                if (sylvanGo != null)
                {
                    var sSr = sylvanGo.GetComponentInChildren<SpriteRenderer>(true);
                    if (sSr != null) sSr.sprite = sylvanProf.sprite;
                }
                PixelCrawlerHubToolkit.BindRoot(scene, "Accord", accordProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "NaveCrowd", crowdProf);

                // Additional ambient clergy/guards in the expanded Nave.
                var barkDialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(BarksPath);
                EnsureAmbientNpc(scene, "Nave_Acolyte_W", new Vector3(-7.4f, -3.4f, 0f), crowdProf, barkDialogue);
                EnsureAmbientNpc(scene, "Nave_Guard_S", new Vector3(-3.4f, -5.6f, 0f), guardProf, barkDialogue);

                PixelCrawlerHubToolkit.BindStaticProp(scene, "ValeriusAltar", "Cath_Altar", IntPropsPath, "Int_Interior_Props_01_05");
                PixelCrawlerHubToolkit.BindStaticProp(scene, "TuningFork", "Cath_Tuning", EsotericPath, "Esoteric_41");

                // Use custom stone-and-crimson Cathedral_Stairs_Up for the Belfry stairwell.
                var stairGo = PixelCrawlerHubToolkit.FindRoot(scene, "BelfryStair");
                if (stairGo != null)
                {
                    var stairSprite = EnsureSingleSprite(BelfryStairsPath, 16f);
                    if (stairSprite != null)
                    {
                        var belfryProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                            "Cath_BelfryStair_Stone", BelfryStairsPath, stairSprite.name, null, Vector2.zero);
                        PixelCrawlerHubToolkit.BindActorVisual(stairGo, belfryProf, addYSort: true);
                    }
                    else
                    {
                        PixelCrawlerHubToolkit.BindStaticProp(scene, "BelfryStair", "Cath_BelfryDoor", DungeonPropsPath, "Dungeon_Props_02");
                    }
                }

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

        static Sprite EnsureSingleSprite(string assetPath, float ppu)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp != null && (imp.textureType != TextureImporterType.Sprite ||
                imp.spriteImportMode != SpriteImportMode.Single ||
                Mathf.Abs(imp.spritePixelsPerUnit - ppu) > 0.01f ||
                imp.filterMode != FilterMode.Point))
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = ppu;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        static void SetPos(Scene scene, string rootName, Vector3 pos)
        {
            var go = PixelCrawlerHubToolkit.FindRoot(scene, rootName);
            if (go != null) go.transform.position = pos;
        }

        static void EnsureAmbientNpc(Scene scene, string name, Vector3 pos, VisualProfile prof, DialogueData dialogue)
        {
            var go = PixelCrawlerHubToolkit.FindRoot(scene, name);
            NpcInteractable npc = go != null ? go.GetComponent<NpcInteractable>() : null;
            if (npc == null)
                HubSceneFactory.CreateInteractable(name, new Color(0.55f, 0.55f, 0.65f), pos, out npc);
            else
                npc.transform.position = pos;

            npc.promptText = "[E] Talk";
            if (dialogue != null) npc.dialogue = dialogue;
            PixelCrawlerHubToolkit.BindActorVisual(npc.gameObject, prof, addYSort: true);
        }
    }
}
