using System.Collections.Generic;
using Morsoul.Dialogue;
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
        const string ResourcesPath = "Assets/Art/Environments/Props/Static/Resources.png";
        const string BlightStumpPath = "Assets/Art/Environments/Custom/Blight_Stump.png";
        const string BlightBushPath = "Assets/Art/Environments/Custom/Blight_Bush.png";
        const string BlightFernPath = "Assets/Art/Environments/Custom/Blight_Fern.png";
        const string BlightPatchPath = "Assets/Art/Environments/Custom/Blight_Patch.png";
        const string BarksPath = "Assets/Data/Dialogue/Case03/D_ForestBarks_03.asset";

        [MenuItem("Morsoul/Scenes/Wilderness", priority = 15)]
        public static void RebuildMenu() => Rebuild();

        public static void Rebuild()
        {
            PixelCrawlerHubToolkit.ClearCache();
            RebuildMainSceneBindings();
            AssetDatabase.SaveAssets();
            Debug.Log("WildernessSceneBuilder: rebuilt unified Hub_Wilderness (Pixel Crawler).");
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
                    ("Bound_North", new Vector2(0f, 5.2f), new Vector2(26f, 1.6f)),
                    ("Bound_South", new Vector2(0f, -6.4f), new Vector2(26f, 1.6f)),
                    ("Bound_West", new Vector2(-11.8f, 0f), new Vector2(1.6f, 14f)),
                    ("Bound_East", new Vector2(11.0f, 0f), new Vector2(1.6f, 14f)));

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

            PixelCrawlerHubToolkit.FillRect(grassMap, grassTile, -22, -14, 22, 14);

            // Whisperwood east-west trail from TrailExit (-9.5,0) to BriarGate (9.6,0) + expanded ranger outpost pads.
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -16, -1, 15, 1);
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -11, -6, -2, -1);
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, -7, 1, -2, 3);
            // Short southern spur to BlightBark inspection clearing.
            PixelCrawlerHubToolkit.FillRect(dirtMap, dirtTile, 0, -3, 2, -1);

            // Light highland frost along distant northern ridge.
            PixelCrawlerHubToolkit.FillRect(snowMap, snowTile, -20, 8, 20, 12);

            // Woodland pond in SE clearing (collider keeps player on shore).
            PixelCrawlerHubToolkit.FillRect(waterMap, waterTile, 5, -7, 9, -4);

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
            var oakDeadA = PixelCrawlerHubToolkit.LoadSprite(TreeM1Path, "Tree_Model_01_Size_03_07");
            var oakDeadB = PixelCrawlerHubToolkit.LoadSprite(TreeM1Path, "Tree_Model_01_Size_03_08");
            var pineTeal = PixelCrawlerHubToolkit.LoadSprite(TreeM2Path, "Tree_Model_02_Size_03_00");
            var pineDark = PixelCrawlerHubToolkit.LoadSprite(TreeM2Path, "Tree_Model_02_Size_03_01");
            var spruceTall = PixelCrawlerHubToolkit.LoadSprite(TreeM3Path, "Tree_Model_03_Size_03_00");

            var bushMed = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_04");
            var bushSmall = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_00");
            var fern = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_12");
            var thornyBush = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_26");
            var reeds = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_33");
            var flower = PixelCrawlerHubToolkit.LoadSprite(VegPath, "Vegetation_34");

            var boulder = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_05");
            var smallRock = PixelCrawlerHubToolkit.LoadSprite(RocksPath, "Rocks_07");

            var logPlank = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_21");
            var crateAlt = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_35");
            var blightBush = AssetDatabase.LoadAssetAtPath<Sprite>(BlightBushPath);
            var blightFern = AssetDatabase.LoadAssetAtPath<Sprite>(BlightFernPath);
            var blightPatch = AssetDatabase.LoadAssetAtPath<Sprite>(BlightPatchPath);

            var treeCol = new Vector2(0.95f, 0.65f);
            var treeOff = new Vector2(0f, 0.35f);
            var rockCol = new Vector2(1.35f, 0.8f);
            var rockOff = new Vector2(0f, 0.4f);
            var bushCol = new Vector2(1.35f, 0.75f);
            var bushOff = new Vector2(0f, 0.38f);

            // Dense Northern Whisperwood treeline (staggered 2-row canopy above y=5.1).
            Sprite[] northPool = { pineTeal, oakGreen, pineDark, oakGold };
            for (int i = 0; i < 10; i++)
            {
                float x = -13.0f + i * 2.85f;
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Tree_N1_{i}", northPool[i % northPool.Length], new Vector2(x, 5.15f + (i % 2) * 0.3f), colliderSize: treeCol, colliderOffset: treeOff);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Tree_N2_{i}", (i % 2 == 0 ? spruceTall : pineDark) ?? pineDark, new Vector2(x + 1.35f, 7.0f));
            }

            // Western & Eastern forest walls (leave east-west trail y in [-1, 1] open for BriarGate trigger).
            foreach (float y in new[] { 3.6f, 1.9f, -2.2f, -4.6f })
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Tree_W_{y:F0}", y > 0 ? pineTeal : oakGreen, new Vector2(-12.2f, y), colliderSize: treeCol, colliderOffset: treeOff);
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Tree_E_{y:F0}", y > 0 ? oakGold : pineDark, new Vector2(11.8f, y), colliderSize: treeCol, colliderOffset: treeOff);
            }

            // Southern deep backdrop trees below y=-7.8 (keep SE pond x=4..10 visible).
            foreach (float x in new[] { -10.5f, -7.5f, -4.5f, -1.5f, 1.5f })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Tree_S_{x:F0}", x < -3f ? pineTeal : oakGreen, new Vector2(x, -8.5f), colliderSize: treeCol, colliderOffset: treeOff);

            // Western trailhead & expanded Ranger camp outpost (NW shelter + SW campfire, supply & prep zones).
            var carrierWagon = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/Carrier_Wagon.png", 16f);
            if (carrierWagon != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Carrier_Wagon", carrierWagon,
                    new Vector2(-9.2f, 0.8f), colliderSize: new Vector2(4.2f, 1.4f), colliderOffset: new Vector2(0f, 0.7f));
            }
            AssetDatabase.ImportAsset("Assets/Art/Environments/Custom/Whisperwood_RangerTent.png",
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var rangerTent = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/Whisperwood_RangerTent.png", 16f);
            if (rangerTent != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Ranger_Outpost_Tent", rangerTent,
                    new Vector2(-4.2f, 2.3f), colliderSize: new Vector2(3.6f, 1.5f), colliderOffset: new Vector2(0f, 0.75f));
                PixelCrawlerHubToolkit.PlaceSoftGlow(root, "Wild_Tent_Glow", new Vector2(-4.2f, 1.6f), new Color(1.0f, 0.78f, 0.42f, 0.22f), scale: 2.8f);
            }
            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "Wild_Campfire_Glow", new Vector2(-5.5f, -2.5f), new Color(1.0f, 0.65f, 0.25f, 0.32f), scale: 3.8f);
            PixelCrawlerHubToolkit.PlaceSoftGlow(root, "Wild_Blight_Glow", new Vector2(1.2f, -2.2f), new Color(0.62f, 0.32f, 0.90f, 0.28f), scale: 3.2f);
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Trailhead_Rock", smallRock, new Vector2(-9.8f, 1.35f), colliderSize: new Vector2(0.8f, 0.55f), colliderOffset: new Vector2(0f, 0.28f));

            // Supply & Storage area (West of campfire) + Work/Prep area (SE of campfire).
            var supplyCrate = PixelCrawlerHubToolkit.LoadSprite(ResourcesPath, "Resources_34");
            var oakBench = PixelCrawlerHubToolkit.LoadSprite("Assets/Art/Environments/Buildings/Interior/Interior_Props_01.png", "Int_Interior_Props_01_99");
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Camp_Crate_W", crateAlt, new Vector2(-9.6f, -2.4f), colliderSize: new Vector2(0.85f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));
            if (supplyCrate != null)
            {
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Camp_Crate_SW", supplyCrate, new Vector2(-9.4f, -4.2f), colliderSize: new Vector2(0.85f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Camp_Tent_Crate", supplyCrate, new Vector2(-6.7f, 1.9f), colliderSize: new Vector2(0.85f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));
            }
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Camp_LogSeat", logPlank, new Vector2(-5.5f, -3.75f), colliderSize: new Vector2(1.7f, 0.45f), colliderOffset: new Vector2(0f, 0.22f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Camp_Supply_Log", logPlank, new Vector2(-3.55f, -2.75f), colliderSize: new Vector2(1.7f, 0.45f), colliderOffset: new Vector2(0f, 0.22f));
            if (oakBench != null)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Camp_Prep_Bench", oakBench, new Vector2(5.8f, -2.2f), colliderSize: new Vector2(1.7f, 0.55f), colliderOffset: new Vector2(0f, 0.25f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Camp_Prep_Crate", crateAlt, new Vector2(-7.6f, -3.9f), colliderSize: new Vector2(0.85f, 0.65f), colliderOffset: new Vector2(0f, 0.33f));

            // BlightBark corrupted clearing (around (1.2, -2.5)) with blighted ground patch & withered flora.
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Blight_GroundPatch", blightPatch, new Vector2(1.2f, -2.25f), zBias: 0.05f, sortingOrder: -8);
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Blight_Thorn", blightBush ?? thornyBush, new Vector2(-0.35f, -2.85f), colliderSize: bushCol, colliderOffset: bushOff);
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Blight_Fern", blightFern ?? fern, new Vector2(2.45f, -2.75f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Blight_Fern_NW", blightFern ?? fern, new Vector2(0.35f, -1.85f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Blight_Rock", smallRock, new Vector2(2.05f, -1.85f), colliderSize: new Vector2(0.8f, 0.55f), colliderOffset: new Vector2(0f, 0.28f), tint: new Color(0.65f, 0.55f, 0.72f, 1f));

            // SE Woodland Pond shoreline framing (pond tiles at x=5..9, y=-7..-4).
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Pond_Boulder_NW", boulder, new Vector2(4.4f, -3.7f), colliderSize: rockCol, colliderOffset: rockOff);
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Pond_Reeds_W", reeds, new Vector2(4.55f, -5.2f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Pond_Reeds_N", reeds, new Vector2(7.0f, -3.35f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Pond_Reeds_E", reeds, new Vector2(9.85f, -5.0f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Pond_Bush_NE", bushMed, new Vector2(9.6f, -3.2f), colliderSize: bushCol, colliderOffset: bushOff);
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Pond_Flower_W", flower, new Vector2(4.1f, -4.45f));

            // East trailhead framing around BriarGate walk-in trigger (9.6, 0.0) — clean & unobstructed (no briar vines).
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Briar_Tree_N", oakDeadA ?? pineDark, new Vector2(10.0f, 2.65f), colliderSize: treeCol, colliderOffset: treeOff);
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Briar_Boulder_N", boulder, new Vector2(7.0f, 1.5f), colliderSize: rockCol, colliderOffset: rockOff);

            // Trailside bushes, ferns, wildflowers & boulders (clear of main path & expanded camp pads).
            var bushes = new (Vector2 pos, Sprite spr)[]
            {
                (new Vector2(-7.8f, 2.7f), bushMed),
                (new Vector2(-0.8f, 2.8f), bushMed),
                (new Vector2(2.6f, 3.1f), bushSmall),
                (new Vector2(6.0f, 2.9f), bushMed),
                (new Vector2(-11.4f, -2.6f), bushSmall),
                (new Vector2(-1.2f, -4.8f), bushMed),
            };
            for (int i = 0; i < bushes.Length; i++)
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Wild_Bush_{i}", bushes[i].spr, bushes[i].pos, colliderSize: bushCol, colliderOffset: bushOff);

            foreach (var pos in new[] { new Vector2(-1.6f, 2.2f), new Vector2(0.8f, 2.3f), new Vector2(5.1f, 2.3f), new Vector2(-2.2f, -2.3f), new Vector2(3.1f, -2.4f) })
                PixelCrawlerHubToolkit.PlaceSpriteProp(root, $"Wild_Flower_{pos.x:F0}_{pos.y:F0}", flower, pos);

            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Wild_Fern_NW", fern, new Vector2(-6.4f, 3.4f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Wild_Fern_SW", fern, new Vector2(-11.2f, -4.8f));
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Wild_Boulder_NW", boulder, new Vector2(-5.4f, 4.0f), colliderSize: rockCol, colliderOffset: rockOff);
            PixelCrawlerHubToolkit.PlaceSpriteProp(root, "Wild_Boulder_N", boulder, new Vector2(1.4f, 3.6f), colliderSize: rockCol, colliderOffset: rockOff);
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
                BuildTerrain(scene);
                BuildProps(scene);
                PixelCrawlerHubToolkit.RebuildBounds(scene, "WildernessBounds",
                    ("Bound_North", new Vector2(0f, 5.2f), new Vector2(26f, 1.6f)),
                    ("Bound_South", new Vector2(0f, -6.4f), new Vector2(26f, 1.6f)),
                    ("Bound_West", new Vector2(-11.8f, 0f), new Vector2(1.6f, 14f)),
                    ("Bound_East", new Vector2(11.0f, 0f), new Vector2(1.6f, 14f)));
                PixelCrawlerHubToolkit.ConfigureCamera(
                    scene, new Vector2(-12.5f, -7.0f), new Vector2(11.5f, 5.8f),
                    5.0f, new Color(0.06f, 0.11f, 0.06f, 1f));

                var playerProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Player", "Assets/Art/Characters/Player/Golem/Idle/Idle_Down-Sheet.png",
                    "Idle_Down-Sheet_0", "Assets/Animations/Player/Player_Golem.controller",
                    new Vector2(0f, -0.35f));
                var kaelenProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Kaelen", "Assets/Art/Characters/NPCs/Citizen_M/Adventurer_B/Idle/Idle_Side-Sheet.png",
                    "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_AdventurerB.controller",
                    new Vector2(0f, -0.35f));
                var vaneProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Vane", "Assets/Art/Characters/NPCs/Named/Vane/Idle/Idle_Down-Sheet.png",
                    "Idle_Down-Sheet_0", "Assets/Animations/NPCs/NPC_Vane.controller",
                    new Vector2(0f, -0.35f), flipX: true);
                var carrierProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                    "Carrier", "Assets/Art/Characters/NPCs/Named/Carrier/Idle/Idle_Down-Sheet.png",
                    "Idle_Down-Sheet_0", "Assets/Animations/NPCs/NPC_Carrier.controller",
                    new Vector2(0f, -0.35f));

                var trailExit = PixelCrawlerHubToolkit.FindRoot(scene, "TrailExit");
                if (trailExit != null) Object.DestroyImmediate(trailExit);

                SetRootPos(scene, "Player", new Vector3(-6.2f, -0.4f, 0f));
                SetRootPos(scene, "PlayerSpawn", new Vector3(-6.2f, -0.4f, 0f));
                SetRootPos(scene, "Carrier", new Vector3(-8.2f, -0.4f, 0f));
                SetRootPos(scene, "RestSpot", new Vector3(-5.5f, -2.8f, 0f));
                SetRootPos(scene, "ForestOutpost", new Vector3(-8.5f, -2.2f, 0f));
                SetRootPos(scene, "Kaelen", new Vector3(-2.2f, 1.3f, 0f));
                SetRootPos(scene, "BlightBark", new Vector3(1.2f, -2.5f, 0f));
                SetRootPos(scene, "Vane", new Vector3(4.2f, 1.4f, 0f));
                SetRootPos(scene, "BriarGate", new Vector3(9.6f, 0.0f, 0f));

                PixelCrawlerHubToolkit.BindRoot(scene, "Player", playerProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "Kaelen", kaelenProf);
                var kaelenGo = PixelCrawlerHubToolkit.FindRoot(scene, "Kaelen");
                if (kaelenGo != null)
                {
                    var kSr = kaelenGo.GetComponentInChildren<SpriteRenderer>(true);
                    if (kSr != null) kSr.sprite = kaelenProf.sprite;
                }
                PixelCrawlerHubToolkit.BindRoot(scene, "Vane", vaneProf);
                PixelCrawlerHubToolkit.BindRoot(scene, "Carrier", carrierProf);

                var barkDialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(BarksPath);
                EnsureAmbientNpc(scene, "Wild_Ranger", new Vector3(-6.8f, -2.4f, 0f),
                    "Assets/Art/Characters/NPCs/Citizen_M/Adventurer_A/Idle/Idle_Side-Sheet.png",
                    "Idle_Side-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenM_AdventurerA.controller",
                    false, barkDialogue);
                EnsureAmbientNpc(scene, "Wild_Herbalist", new Vector3(3.6f, -2.9f, 0f),
                    "Assets/Art/Characters/NPCs/Citizen_F/Peasant_A/Idle/Idle-Sheet.png",
                    "Idle-Sheet_0", "Assets/Animations/NPCs/NPC_CitizenF_PeasantA.controller",
                    true, barkDialogue);

                PixelCrawlerHubToolkit.BindStaticProp(scene, "RestSpot", "Wild_Campfire", BonfirePath, "Bonfire_00");
                PixelCrawlerHubToolkit.BindStaticProp(scene, "ForestOutpost", "Wild_OutpostCrates", ResourcesPath, "Resources_34");
                var fallenLog = PixelCrawlerHubToolkit.LoadCustomSprite("Assets/Art/Environments/Custom/Whisperwood_FallenBlightLog.png", 16f);
                if (fallenLog != null)
                {
                    var bbGo = PixelCrawlerHubToolkit.FindRoot(scene, "BlightBark");
                    if (bbGo != null)
                    {
                        var bbProf = PixelCrawlerHubToolkit.EnsureCharacterProfile(
                            "Wild_FallenBlightLog", "Assets/Art/Environments/Custom/Whisperwood_FallenBlightLog.png",
                            fallenLog.name, null, Vector2.zero);
                        PixelCrawlerHubToolkit.BindActorVisual(bbGo, bbProf, addYSort: true);
                    }
                }
                else
                {
                    PixelCrawlerHubToolkit.BindStaticProp(scene, "BlightBark", "Wild_BlightStump", BlightStumpPath, "Blight_Stump");
                }

                EnsureSolidBody(scene, "RestSpot", new Vector2(1.0f, 0.65f), new Vector2(0f, 0.35f));
                var restGo = PixelCrawlerHubToolkit.FindRoot(scene, "RestSpot");
                if (restGo != null)
                {
                    foreach (var c in restGo.GetComponents<BoxCollider2D>())
                    {
                        if (!c.isTrigger) continue;
                        c.size = new Vector2(2.4f, 2.2f);
                        c.offset = new Vector2(0f, 0.4f);
                        break;
                    }
                }
                EnsureSolidBody(scene, "ForestOutpost", new Vector2(0.9f, 0.7f), new Vector2(0f, 0.35f));
                EnsureSolidBody(scene, "BlightBark", new Vector2(0.95f, 0.65f), new Vector2(0f, 0.35f));

                var briarGo = PixelCrawlerHubToolkit.FindRoot(scene, "BriarGate");
                if (briarGo != null)
                {
                    var gate = briarGo.GetComponent<GateInteractable>();
                    if (gate != null)
                    {
                        gate.triggerOnEnter = true;
                        gate.promptText = "";
                    }
                    var col = briarGo.GetComponent<BoxCollider2D>();
                    if (col != null)
                    {
                        col.isTrigger = true;
                        col.size = new Vector2(1.8f, 2.2f);
                    }
                    var vis = briarGo.transform.Find("Visual");
                    if (vis != null) Object.DestroyImmediate(vis.gameObject);
                    var ev = briarGo.GetComponent<EntityVisual>();
                    if (ev != null) Object.DestroyImmediate(ev);
                }
                PixelCrawlerHubToolkit.HideTriggerVisual(scene, "BriarGate");
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

        static void SetRootPos(Scene scene, string name, Vector3 pos)
        {
            var go = PixelCrawlerHubToolkit.FindRoot(scene, name);
            if (go != null) go.transform.position = pos;
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
                HubSceneFactory.CreateInteractable(name, new Color(0.55f, 0.65f, 0.55f), pos, out NpcInteractable npc);
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
