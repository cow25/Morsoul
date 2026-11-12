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
                    ("Bound_North", new Vector2(0f, 7.2f), new Vector2(40f, 1.4f)),
                    ("Bound_South", new Vector2(0f, -8.5f), new Vector2(40f, 1.4f)),
                    ("Bound_West", new Vector2(-18.5f, -0.6f), new Vector2(1.6f, 17.5f)),
                    ("Bound_East", new Vector2(18.5f, -0.6f), new Vector2(1.6f, 17.5f)));

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

            // Slate mine cavern flagstones across 3x expanded mining cavern (no edge voids).
            PixelCrawlerHubToolkit.FillRandomStaticBlock(floorMap, DungeonSheet, -26, -14, 26, 13, 0, 2, 4, 7);

            // Framed wooden mine work platforms under DrillBit pit, central forge, & DeepShaftLift bay.
            StampWoodDeck(deckMap, -15, 2, 5, 4);
            StampWoodDeck(deckMap, -4, 3, 8, 3);
            StampWoodDeck(deckMap, 10, 2, 6, 4);

            // Northern craggy slate cavern cliff face (Wall_Variations slate r=10..12).
            for (int x = -26; x <= 26; x++)
            {
                for (int dy = 0; dy < 3; dy++)
                {
                    int r = 12 - dy;
                    int c = Mathf.Abs(x) % 8;
                    var wt = PixelCrawlerHubToolkit.LoadStaticTile(WallVarSheet, $"{WallVarSheet}_{r}_{c}");
                    if (wt != null) cliffMap.SetTile(new Vector3Int(x, 7 + dy, 0), wt);
                }
            }

            // Three timber-braced drift tunnel portals carved into north cavern cliff (r=15..17, cols 12..15).
            StampTimberTunnel(cliffMap, startX: -15, baseY: 7);
            StampTimberTunnel(cliffMap, startX: -2, baseY: 7);
            StampTimberTunnel(cliffMap, startX: 11, baseY: 7);

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
            var woodWorkbench = PixelCrawlerHubToolkit.LoadSprite(WorkbenchPath, "Workbench_Wood_Med");
            var steelWorkbench = PixelCrawlerHubToolkit.LoadSprite(WorkbenchPath, "Workbench_Steel_Med");
            var leverBank = PixelCrawlerHubToolkit.LoadSprite(DungeonPropsPath, "Dungeon_Props_00");
            var coalLarge = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_02");
            var coalCrate = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_35");
            var supplyCrate = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_34");
            var boulder = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_05");
            var boulderDark = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_01");
            var spireStone = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_07");
            var rockCluster = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_11");
            var campBench = PixelCrawlerHubToolkit.LoadSprite(ExtPropsPath, "Ext_Props_14");

            // Central Smelter & Forge Plaza along northern cliff.
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Furnace_Main", ironFurnace,
                new Vector2(1.8f, 5.6f), colliderSize: new Vector2(2.2f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Furnace_Brick", brickFurnace,
                new Vector2(-1.8f, 5.6f), colliderSize: new Vector2(2.2f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Anvil_Central", anvil,
                new Vector2(4.2f, 4.6f), colliderSize: new Vector2(1.1f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Workbench_Central", steelWorkbench,
                new Vector2(-4.4f, 4.6f), colliderSize: new Vector2(2.6f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));

            // Western Excavation & Drill Pit stations.
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_DrillControls", leverBank,
                new Vector2(-12.5f, 5.2f), colliderSize: new Vector2(2.8f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Workbench_West", woodWorkbench,
                new Vector2(-8.6f, 4.6f), colliderSize: new Vector2(2.6f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Anvil_West", anvil,
                new Vector2(-10.2f, 3.3f), colliderSize: new Vector2(1.1f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));

            // Eastern Deep Shaft Lift staging stations.
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_LiftControls", leverBank,
                new Vector2(9.6f, 5.2f), colliderSize: new Vector2(2.8f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Workbench_East", steelWorkbench,
                new Vector2(6.8f, 4.6f), colliderSize: new Vector2(2.6f, 1.0f), colliderOffset: new Vector2(0f, 0.5f));

            // Campfire rest bench (behind the campfire in SW camp).
            if (campBench != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_CampBench_N", campBench,
                    new Vector2(-10.5f, -3.2f), colliderSize: new Vector2(2.8f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));

            // Coal & iron ore stockpiles across all 3 wings.
            foreach (var pos in new[] {
                new Vector2(-5.5f, 5.8f), new Vector2(5.2f, 3.0f),
                new Vector2(-15.5f, 5.4f), new Vector2(-7.8f, 0.8f),
                new Vector2(16.0f, 4.8f), new Vector2(14.5f, -4.8f),
                new Vector2(-15.8f, -6.4f), new Vector2(0.8f, -6.5f)
            })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_Coal_{pos.x:F0}_{pos.y:F0}", coalLarge, pos,
                    colliderSize: new Vector2(1.6f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));

            foreach (var pos in new[] {
                new Vector2(-0.5f, -6.4f), new Vector2(5.6f, 5.5f), new Vector2(-16.2f, 1.2f),
                new Vector2(8.8f, -0.4f), new Vector2(10.0f, -5.8f), new Vector2(-5.2f, -5.8f)
            })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_OreCrate_{pos.x:F0}_{pos.y:F0}", coalCrate, pos,
                    colliderSize: new Vector2(0.85f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));

            // Supply & ore crate clusters.
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Supply_SW", supplyCrate,
                new Vector2(-14.2f, -6.8f), colliderSize: new Vector2(0.95f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Supply_SW2", coalCrate,
                new Vector2(-13.1f, -6.85f), colliderSize: new Vector2(0.85f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Supply_SE", supplyCrate,
                new Vector2(12.2f, -6.5f), colliderSize: new Vector2(0.95f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Supply_SE2", coalCrate,
                new Vector2(13.3f, -6.55f), colliderSize: new Vector2(0.85f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Iron_Supply_Mid", supplyCrate,
                new Vector2(3.6f, -6.5f), colliderSize: new Vector2(0.95f, 0.7f), colliderOffset: new Vector2(0f, 0.35f));

            // Slag boulders & cavern rock formations framing the 3x perimeter and work pits.
            foreach (var pos in new[] {
                new Vector2(-17.5f, 4.5f), new Vector2(-17.5f, 0.0f), new Vector2(-17.5f, -4.5f),
                new Vector2(17.5f, 4.2f), new Vector2(17.5f, -0.5f), new Vector2(17.5f, -4.5f),
                new Vector2(-7.0f, -7.2f), new Vector2(-2.6f, -7.2f), new Vector2(6.5f, -7.2f), new Vector2(16.2f, -7.0f),
                new Vector2(-6.8f, 2.2f), new Vector2(6.8f, 1.8f)
            })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_Rock_{pos.x:F0}_{pos.y:F0}", boulder ?? boulderDark, pos,
                    colliderSize: new Vector2(1.35f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));

            if (spireStone != null)
            {
                foreach (var pos in new[] { new Vector2(-17.2f, -2.2f), new Vector2(17.2f, 2.0f), new Vector2(1.8f, -7.3f) })
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_Spire_{pos.x:F0}_{pos.y:F0}", spireStone, pos,
                        colliderSize: new Vector2(1.2f, 0.8f), colliderOffset: new Vector2(0f, 0.4f));
            }
            if (rockCluster != null)
            {
                foreach (var pos in new[] { new Vector2(-12.0f, 1.5f), new Vector2(8.0f, -3.5f), new Vector2(-3.5f, -4.5f) })
                    PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Iron_Cluster_{pos.x:F0}_{pos.y:F0}", rockCluster, pos,
                        colliderSize: new Vector2(0.9f, 0.6f), colliderOffset: new Vector2(0f, 0.3f));
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
                    scene, new Vector2(-19.0f, -9.0f), new Vector2(19.0f, 8.0f),
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

                SetRootPos(scene, "Player", new Vector3(0f, 0f, 0f));
                SetRootPos(scene, "Carrier", new Vector3(-15.6f, 3.2f, 0f));
                SetRootPos(scene, "RestSpot", new Vector3(-10.5f, -5.0f, 0f));
                SetRootPos(scene, "DrillBit", new Vector3(-12.5f, 3.6f, 0f));
                SetRootPos(scene, "Thrun", new Vector3(2.4f, 3.6f, 0f));
                SetRootPos(scene, "Harlan", new Vector3(4.8f, 0.6f, 0f));
                SetRootPos(scene, "MinerCrowd", new Vector3(-8.8f, -4.6f, 0f));
                SetRootPos(scene, "DeepShaftLift", new Vector3(13.0f, 3.8f, 0f));

                PixelCrawlerHubToolkit.BindRoot(scene, "Player", playerProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "Thrun", thrunProf);
                ForceSprite(scene, "Thrun", thrunProf.sprite);
                PixelCrawlerHubToolkit.BindRoot(scene, "Harlan", harlanProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "MinerCrowd", minerProf);
                ForceSprite(scene, "MinerCrowd", minerProf.sprite);
                PixelCrawlerHubToolkit.BindRoot(scene, "Carrier", carrierProf);

                var barkDialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(BarksPath);
                EnsureAmbientNpc(scene, "Iron_Watchman", new Vector3(15.6f, 1.4f, 0f),
                    "Assets/Art/Characters/NPCs/Citizen_M/Guard_A/Idle/Idle_Side-Sheet.png",
                    "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_GuardA.controller",
                    true, barkDialogue);

                // Populate 3x expanded mining cavern with male & female miners.
                EnsureAmbientNpc(scene, "Miner_Camp_M", new Vector3(-12.1f, -4.6f, 0f),
                    MinerMSheet, "Idle_Side-Sheet_0", MinerMCtrl, false, barkDialogue);
                EnsureAmbientNpc(scene, "Miner_West_M", new Vector3(-9.8f, 1.6f, 0f),
                    MinerMSheet, "Idle_Side-Sheet_0", MinerMCtrl, true, barkDialogue);
                EnsureAmbientNpc(scene, "Miner_West_F", new Vector3(-15.2f, -1.4f, 0f),
                    MinerFSheet, "Idle-Sheet_0", MinerFCtrl, false, barkDialogue);
                EnsureAmbientNpc(scene, "Miner_Central_F", new Vector3(-3.6f, -2.2f, 0f),
                    MinerFSheet, "Idle-Sheet_0", MinerFCtrl, false, barkDialogue);
                EnsureAmbientNpc(scene, "Miner_South_M", new Vector3(2.4f, -5.2f, 0f),
                    MinerMSheet, "Idle_Side-Sheet_0", MinerMCtrl, true, barkDialogue);
                EnsureAmbientNpc(scene, "Miner_East_F", new Vector3(9.2f, 2.2f, 0f),
                    MinerFSheet, "Idle-Sheet_0", MinerFCtrl, false, barkDialogue);
                EnsureAmbientNpc(scene, "Miner_East_M", new Vector3(11.6f, -2.6f, 0f),
                    MinerMSheet, "Idle_Side-Sheet_0", MinerMCtrl, false, barkDialogue);

                PixelCrawlerHubToolkit.BindStaticProp(scene, "DrillBit", "Iron_DrillBlade", ToolsPath, "Tools_25");
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

