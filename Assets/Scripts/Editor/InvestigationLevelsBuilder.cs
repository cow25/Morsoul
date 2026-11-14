using System.Collections.Generic;
using Morsoul.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Editor
{
    /// <summary>
    /// Generates the 3D investigation scenes for Cases 1–4 and the Case 5
    /// Sanctum Core cutscene staging scene from the Case specs.
    /// </summary>
    public static class InvestigationLevelsBuilder
    {
        const string SharedRootPath = "Assets/Prefabs/3DSharedRoot/SharedRoot.prefab";
        const string Lpd = "Assets/[Import] LowPolyDungeonFree/Prefabs/";
        const string Mfp = "Assets/Mega Fantasy Props Pack/Prefabs/";
        const string Dfk = "Assets/RunemarkStudio/DarkFantasyKit [Free]/Prefabs/";
        const string Mtp = "Assets/MedievalTavernPack/Prefabs/";
        const string Bushes = "Assets/YughuesFreeBushes2018/Prefabs/";

        public const string Case1ScenePath = "Assets/Scenes/3D/Case1UndegroundStorageVault.unity";
        public const string Case2ScenePath = "Assets/Scenes/3D/Case2CathedralBelfry.unity";
        public const string Case3ScenePath = "Assets/Scenes/3D/Case3WhisperwoodGrove.unity";
        public const string Case4ScenePath = "Assets/Scenes/3D/Case4IronHollowSubFacility.unity";
        public const string Case5ScenePath = "Assets/Scenes/3D/Case5SanctumCore.unity";

        const int InteractableLayer = 6;

        [MenuItem("Morsoul/3D/Build All Investigation Levels (Cases 1-5)")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("InvestigationLevelsBuilder: Exit PlayMode before building scenes.");
                return;
            }

            BuildCase1Vault();
            BuildCase2Belfry();
            BuildCase3Whisperwood();
            BuildCase4IronHollow();
            BuildCase5Sanctum();
            RegisterBuildScenes(Case1ScenePath, Case2ScenePath, Case3ScenePath, Case4ScenePath, Case5ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("InvestigationLevelsBuilder: built Cases 1-5 3D scenes and registered them in Build Settings.");
        }

        [MenuItem("Morsoul/3D/Build Case 1 - Underground Storage Vault")]
        public static void BuildCase1Vault()
        {
            var scene = CreateBaseScene(
                Case1ScenePath,
                new Color(0.16f, 0.15f, 0.22f),
                new Color(0.08f, 0.07f, 0.12f),
                0.025f,
                new Vector3(0f, 1.0f, -2.5f),
                Vector3.zero);

            var env = new GameObject("Environment").transform;
            SceneManager.MoveGameObjectToScene(env.gameObject, scene);

            // Underground vault: disable default outdoor Directional Light so no sky sunlight leaks through
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) l.gameObject.SetActive(false);
            }
            RenderSettings.skybox = null;

            // Invisible collision bounds (20x20, height 4.5) so player never clips out
            CreateWall("FloorCollider", new Vector3(0f, -0.25f, 4f), new Vector3(20f, 0.5f, 20f), null, env);
            CreateWall("CeilingCollider", new Vector3(0f, 4.3f, 4f), new Vector3(20f, 0.5f, 20f), null, env);
            BuildPerimeterColliders(env, new Vector3(0f, 0f, 4f), new Vector3(20f, 4.5f, 20f));

            // 4m x 4m LowPolyDungeonFree stone floor tiles (-8..8 in X, -4..12 in Z)
            for (int gx = -8; gx <= 8; gx += 4)
            for (int gz = -4; gz <= 12; gz += 4)
            {
                string tile = Lpd + "02_Floors/SM_LPD_Floor_PavingFlagstone_01.prefab";
                if (gx == 4 && gz == 4) tile = Lpd + "02_Floors/SM_LPD_Floor_PavingDrain_01.prefab";
                else if (gx == 0 && gz == 8) tile = Lpd + "02_Floors/SM_LPD_Floor_PavingHatchLidded_01.prefab";
                else if (gx == 4 && gz == 0) tile = Lpd + "02_Floors/SM_LPD_Floor_PavingChannelStraight_01.prefab";
                else if ((gx + gz) % 8 == 0) tile = Lpd + "02_Floors/SM_LPD_Floor_PavingFlagstoneWorn_01.prefab";
                PlaceProp(tile, env, new Vector3(gx, 0f, gz), Vector3.zero, Vector3.one);
            }

            // Flat stone roof at Y = 4.0
            for (int gx = -8; gx <= 8; gx += 4)
            for (int gz = -4; gz <= 12; gz += 4)
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_CeilFlat_01.prefab", env, new Vector3(gx, 4.0f, gz), Vector3.zero, Vector3.one);

            // Sealed 4m x 4m perimeter stone walls (x = +/-10, z = -6 / +14)
            for (int gx = -8; gx <= 8; gx += 4)
            {
                string northWall = gx == 0 ? "01_Structure/SM_LPD_Struct_WallDoorway_01.prefab" : "01_Structure/SM_LPD_Struct_WallAshlar_01.prefab";
                PlaceProp(Lpd + northWall, env, new Vector3(gx, 0f, 14f), new Vector3(0f, 180f, 0f), Vector3.one);
                string southWall = gx == 0 ? "01_Structure/SM_LPD_Struct_WallDoorway_01.prefab" : "01_Structure/SM_LPD_Struct_WallCoursed_01.prefab";
                PlaceProp(Lpd + southWall, env, new Vector3(gx, 0f, -6f), Vector3.zero, Vector3.one);
            }
            for (int gz = -4; gz <= 12; gz += 4)
            {
                string wWall = gz == 4 ? "01_Structure/SM_LPD_Struct_WallNiche_01.prefab" : "01_Structure/SM_LPD_Struct_WallAshlar_01.prefab";
                PlaceProp(Lpd + wWall, env, new Vector3(-10f, 0f, gz), new Vector3(0f, 90f, 0f), Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallCoursed_01.prefab", env, new Vector3(10f, 0f, gz), new Vector3(0f, -90f, 0f), Vector3.one);
            }
            // Quoined corners
            foreach (var cPos in new[] { new Vector3(-10f, 0f, -6f), new Vector3(10f, 0f, -6f), new Vector3(-10f, 0f, 14f), new Vector3(10f, 0f, 14f) })
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_CornerPostQuoined_01.prefab", env, cPos, Vector3.zero, Vector3.one);

            // Structural ornate pillars framing the vault bay
            foreach (var colPos in new[] { new Vector3(-4f, 0f, 0f), new Vector3(4f, 0f, 0f), new Vector3(-4f, 0f, 8f), new Vector3(4f, 0f, 8f) })
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_PillarOrnate_01.prefab", env, colPos, Vector3.zero, Vector3.one);

            // South entry doorway + closed exit door + compact half-stair step
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorFrameStone_01.prefab", env, new Vector3(0f, 0f, -6f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorPlank_01.prefab", env, new Vector3(0f, 0f, -6f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_StairHalfFlight_01.prefab", env, new Vector3(0f, 0f, -5.2f), Vector3.zero, new Vector3(0.8f, 0.5f, 0.6f));
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_SconceIronLit_01.prefab", env, new Vector3(-2.2f, 2.2f, -5.7f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_SconceIronLit_01.prefab", env, new Vector3(2.2f, 2.2f, -5.7f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_ChandelierRing_01.prefab", env, new Vector3(0f, 3.6f, 2.0f), Vector3.zero, Vector3.one);
            AddPointLight(env, new Vector3(0f, 3.2f, 2.0f), new Color(1f, 0.78f, 0.52f), 3.2f, 16f);
            AddPointLight(env, new Vector3(0f, 2.6f, -4.5f), new Color(1f, 0.68f, 0.38f), 2.5f, 10f);

            // Dense merchant-alchemist storage vault dressing (keeping central aisle x=-2..2 clear)
            var storage = new GameObject("VaultStorage").transform;
            storage.SetParent(env, false);

            // SW Pallet & Shipping Bay (x = -8.8..-5.8, z = -4.2..1.2)
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_DunnagePallet_01.prefab", storage, new Vector3(-7.5f, 0f, -3.2f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_CrateLarge_01.prefab", storage, new Vector3(-8.0f, 0.16f, -3.5f), new Vector3(0f, 4f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_CrateBanded_01.prefab", storage, new Vector3(-6.8f, 0.16f, -3.2f), new Vector3(0f, -8f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_CrateMedium_01.prefab", storage, new Vector3(-7.9f, 1.15f, -3.4f), new Vector3(0f, 14f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_SackUpright_01.prefab", storage, new Vector3(-5.8f, 0f, -3.8f), new Vector3(0f, 20f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_SackSlumped_01.prefab", storage, new Vector3(-5.3f, 0f, -3.1f), new Vector3(0f, -30f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_HandcartLoaded_01.prefab", storage, new Vector3(-6.2f, 0f, -1.2f), new Vector3(0f, 38f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_CupboardClosed_01.prefab", storage, new Vector3(-9.2f, 0f, -0.5f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_CupboardOpen_01.prefab", storage, new Vector3(-9.2f, 0f, 1.8f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_CrateLarge_01.prefab", storage, new Vector3(-8.2f, 0f, 4.2f), new Vector3(0f, 12f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_CrateSmall_01.prefab", storage, new Vector3(-8.2f, 1.0f, 4.2f), new Vector3(0f, -15f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_PotClay_01.prefab", storage, new Vector3(-7.4f, 0f, 5.4f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_UrnUrn_01.prefab", storage, new Vector3(-8.4f, 0f, 5.8f), Vector3.zero, Vector3.one);

            // SE Barrel & Cask Bay (x = 5.8..9.2, z = -4.2..2.5)
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_DunnageStillage_01.prefab", storage, new Vector3(7.6f, 0f, -3.5f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_BarrelRackSingle_01.prefab", storage, new Vector3(8.2f, 0f, -3.8f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_BarrelRackSingle_01.prefab", storage, new Vector3(8.2f, 0f, -2.2f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_BarrelUpright_01.prefab", storage, new Vector3(6.4f, 0f, -4.0f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_BarrelUpright_01.prefab", storage, new Vector3(6.4f, 0f, -2.9f), new Vector3(0f, 40f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_BarrelSide_01.prefab", storage, new Vector3(7.8f, 0f, -0.4f), new Vector3(0f, 15f, 0f), Vector3.one);
            PlaceProp(Mfp + "Complex prefabs/Boxes.prefab", storage, new Vector3(8.0f, 0f, 1.4f), new Vector3(0f, -22f, 0f), Vector3.one);
            PlaceProp(Mfp + "Storage/crate.001.prefab", storage, new Vector3(6.4f, 0f, 1.8f), new Vector3(0f, 35f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_BarrelCracked_01.prefab", storage, new Vector3(6.0f, 0f, 3.6f), new Vector3(0f, -45f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_CrateOpen_01.prefab", storage, new Vector3(5.8f, 0f, 5.2f), new Vector3(0f, 22f, 0f), Vector3.one);
            PlaceProp(Mfp + "Miscellaneous/cauldron.prefab", storage, new Vector3(7.2f, 0f, 5.2f), new Vector3(12f, 0f, -18f), Vector3.one);

            // NW & NE Racks, Shelving, Long Tables & Strongboxes (z = 7.5..12.8)
            PlaceProp(Dfk + "Furnitures/dfk_shelf_01.prefab", storage, new Vector3(-9.0f, 0f, 7.8f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Mfp + "Storage/shelf.prefab", storage, new Vector3(-9.0f, 0f, 10.6f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_ShelfPlank_01.prefab", storage, new Vector3(-6.8f, 0f, 13.0f), new Vector3(0f, 180f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_TableLong_01.prefab", storage, new Vector3(-6.4f, 0f, 10.2f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_CrateSmall_01.prefab", storage, new Vector3(-6.9f, 0.78f, 10.2f), new Vector3(0f, 10f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_PotClay_01.prefab", storage, new Vector3(-5.9f, 0.78f, 10.1f), Vector3.zero, Vector3.one);
            PlaceProp(Dfk + "Containers/Chests/dfk_chest_02_closed.prefab", storage, new Vector3(-3.0f, 0f, 12.6f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_ChestFlat_01.prefab", storage, new Vector3(-4.6f, 0f, 12.6f), new Vector3(0f, 15f, 0f), Vector3.one);

            PlaceProp(Dfk + "Furnitures/dfk_shelf_01.prefab", storage, new Vector3(9.0f, 0f, 7.8f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Mfp + "Storage/shelf.prefab", storage, new Vector3(9.0f, 0f, 10.6f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_ShelfPlank_01.prefab", storage, new Vector3(6.8f, 0f, 13.0f), new Vector3(0f, 180f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_TableTrestle_01.prefab", storage, new Vector3(6.4f, 0f, 10.2f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_ChestSmall_01.prefab", storage, new Vector3(6.8f, 0.78f, 10.2f), new Vector3(0f, -18f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_SackOpen_01.prefab", storage, new Vector3(5.6f, 0f, 12.2f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_CrateBanded_01.prefab", storage, new Vector3(3.4f, 0f, 12.5f), new Vector3(0f, -10f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_CrateMedium_01.prefab", storage, new Vector3(3.4f, 0.9f, 12.5f), new Vector3(0f, 14f, 0f), Vector3.one);

            // Clues from Case_01_Vault.md
            var clues = new GameObject("Clues").transform;
            SceneManager.MoveGameObjectToScene(clues.gameObject, scene);

            // C1_01: Pristine Vault Lock (0, 14) — Sealed into the North wall doorway
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorFrameStone_01.prefab", env, new Vector3(0f, 0f, 14f), new Vector3(0f, 180f, 0f), Vector3.one);
            var lockVisual = PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorStudded_01.prefab", clues, new Vector3(0f, 0f, 14f), new Vector3(0f, 180f, 0f), Vector3.one);
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorFittingPadlock_01.prefab", clues, new Vector3(-0.35f, 1.15f, 13.82f), new Vector3(0f, 180f, 0f), Vector3.one * 1.4f);
            AddPointLight(env, new Vector3(0f, 2.8f, 12.6f), new Color(0.72f, 0.86f, 1f), 2.4f, 9f);
            CreateClueNode("C1_PristineVaultLock", "C1_01", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Heavy Dwarven multi-pin cylinder lock. Tumblers and pins are pristine with zero tool marks, shearing, or acid burns.",
                new Vector3(0f, 1.2f, 13.7f), new Vector3(1.8f, 2.4f, 1.0f), clues, lockVisual);

            // C2_01: Vaporized Solvent Residue (4, 4) — drainage floor tile + purple puddle + ambient vapor
            var puddle = CreatePrimitiveMarker("SolventPuddle", PrimitiveType.Cylinder, new Vector3(4f, 0.04f, 4f),
                new Vector3(1.5f, 0.02f, 1.5f), new Color(0.58f, 0.22f, 0.92f), clues, false);
            AddAmbientMist(puddle.transform, new Color(0.65f, 0.28f, 0.98f, 0.45f));
            AddPointLight(env, new Vector3(4f, 1.2f, 4f), new Color(0.68f, 0.28f, 1f), 2.6f, 7f);
            CreateClueNode("C2_SolventResidue", "C2_01", ClueNode.Kind.Physical, ClueNode.Tool.GolemEye, ClueNode.State.Hidden,
                "Pungent, rapid-sublimating synthetic solvent pooling in the drainage gutter — harmless purple vapor, not dark magic.",
                new Vector3(4f, 0.4f, 4f), new Vector3(1.8f, 1.1f, 1.8f), clues, puddle);

            // C3_01: Altered Cargo Ledger (-4.5, 3.8) — Alchemist's Work Desk + candlelight
            PlaceProp(Dfk + "Furnitures/dfk_desk_02.prefab", env, new Vector3(-4.5f, 0f, 3.8f), new Vector3(0f, 15f, 0f), Vector3.one);
            PlaceProp(Mfp + "Chairs/chair.001.prefab", env, new Vector3(-4.5f, 0f, 2.7f), new Vector3(0f, 195f, 0f), Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_CandleTriple_01.prefab", env, new Vector3(-5.0f, 0.85f, 4.0f), Vector3.zero, Vector3.one);
            AddPointLight(env, new Vector3(-4.5f, 1.8f, 3.8f), new Color(1f, 0.72f, 0.36f), 2.4f, 8f);
            var scroll = PlaceProp(Mfp + "Miscellaneous/scrolls.prefab", clues, new Vector3(-4.3f, 0.86f, 3.7f), new Vector3(0f, -20f, 0f), Vector3.one);
            CreateClueNode("C3_AlteredCargoLedger", "C3_01", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Shipment #408 ledger: 200 crates of Greater Restoration Draughts logged, yet zero Moon-Lily extract was ever purchased.",
                new Vector3(-4.4f, 1.0f, 3.7f), new Vector3(1.6f, 1.1f, 1.3f), clues, scroll);

            // L1_01: Sub-floor potion rack alcove (0, 8) — lidded hatch tile, requires AetherSight post-Case 4
            AddPointLight(env, new Vector3(0f, 1.2f, 8f), new Color(0.25f, 0.85f, 0.95f), 1.4f, 5.5f);
            CreateClueNode("L1_RunicLog1_Alcove", "L1_01", ClueNode.Kind.Physical, ClueNode.Tool.AetherSight, ClueNode.State.Hidden,
                "Sub-floor alcove etching: pre-cataclysm equations for synthetic mana solvent volatility (Runic Log #1).",
                new Vector3(0f, 0.4f, 8f), new Vector3(1.8f, 1.2f, 1.8f), clues);

            // Exit stairwell back to Lower Market
            CreateExitPortal("Exit_VaultStairwell", 1, "Hub_LowerMarket",
                "[L-Click] Ascend to Lower Market",
                "Return up the vault stairwell to Asterveil's Lower Market.",
                new Vector3(0f, 1.2f, -5.0f), new Vector3(2.4f, 2.4f, 1.8f), clues);

            // Dynamic Active Goal controller for Case 1
            var ctrlGo = new GameObject("Case1ObjectiveController");
            SceneManager.MoveGameObjectToScene(ctrlGo, scene);
            var objCtrl = ctrlGo.AddComponent<InvestigationObjectiveController>();
            objCtrl.caseId = 1;
            objCtrl.requiredClueIds = new[] { "C1_01", "C2_01", "C3_01" };

            SaveScene(scene, Case1ScenePath);
        }

        [MenuItem("Morsoul/3D/Build Case 2 - Cathedral Belfry")]
        public static void BuildCase2Belfry()
        {
            var scene = CreateBaseScene(
                Case2ScenePath,
                new Color(0.16f, 0.18f, 0.26f),
                new Color(0.09f, 0.10f, 0.17f),
                0.02f,
                new Vector3(0f, 1.0f, -1.5f),
                Vector3.zero);

            var env = new GameObject("Environment").transform;
            SceneManager.MoveGameObjectToScene(env.gameObject, scene);

            // Cool twilight directional moonlight angling through lancet windows
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    l.color = new Color(0.55f, 0.68f, 0.95f);
                    l.intensity = 0.7f;
                }
            }

            // Invisible collision bounds (20x16, height 8.5)
            CreateWall("FloorCollider", new Vector3(0f, -0.25f, 4f), new Vector3(20f, 0.5f, 16f), null, env);
            CreateWall("CeilingCollider", new Vector3(0f, 8.3f, 4f), new Vector3(20f, 0.5f, 16f), null, env);
            BuildPerimeterColliders(env, new Vector3(0f, 0f, 4f), new Vector3(20f, 8.5f, 16f));

            // 4m x 4m Lower Floor (-8..8 in X, -2..10 in Z -> 5x4 grid centered on x=0)
            for (int gx = -8; gx <= 8; gx += 4)
            for (int gz = -2; gz <= 10; gz += 4)
            {
                string tile = (gx + gz) % 8 == 0
                    ? Lpd + "02_Floors/SM_LPD_Floor_PavingFlagstoneWorn_01.prefab"
                    : Lpd + "02_Floors/SM_LPD_Floor_PavingFlagstone_01.prefab";
                PlaceProp(tile, env, new Vector3(gx, 0f, gz), Vector3.zero, Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_CeilFlat_01.prefab", env, new Vector3(gx, 8.0f, gz), Vector3.zero, Vector3.one);
            }

            // Two-story 4m x 4m stone tower walls (x = +/-10, z = -4 / +12)
            for (int gx = -8; gx <= 8; gx += 4)
            {
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallAshlar_01.prefab", env, new Vector3(gx, 0f, 12f), new Vector3(0f, 180f, 0f), Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallWindowLancet_01.prefab", env, new Vector3(gx, 4f, 12f), new Vector3(0f, 180f, 0f), Vector3.one);

                string southLower = gx == 0 ? "01_Structure/SM_LPD_Struct_WallDoorway_01.prefab" : "01_Structure/SM_LPD_Struct_WallCoursed_01.prefab";
                PlaceProp(Lpd + southLower, env, new Vector3(gx, 0f, -4f), Vector3.zero, Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallWindowTwin_01.prefab", env, new Vector3(gx, 4f, -4f), Vector3.zero, Vector3.one);
            }
            for (int gz = -2; gz <= 10; gz += 4)
            {
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallCoursed_01.prefab", env, new Vector3(-10f, 0f, gz), new Vector3(0f, 90f, 0f), Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallWindowLancet_01.prefab", env, new Vector3(-10f, 4f, gz), new Vector3(0f, 90f, 0f), Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallCoursed_01.prefab", env, new Vector3(10f, 0f, gz), new Vector3(0f, -90f, 0f), Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallWindowLancet_01.prefab", env, new Vector3(10f, 4f, gz), new Vector3(0f, -90f, 0f), Vector3.one);
            }
            foreach (var cPos in new[] { new Vector3(-10f, 0f, -4f), new Vector3(10f, 0f, -4f), new Vector3(-10f, 0f, 12f), new Vector3(10f, 0f, 12f) })
            {
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_CornerPostQuoined_01.prefab", env, cPos, Vector3.zero, Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_CornerPostQuoined_01.prefab", env, cPos + Vector3.up * 4f, Vector3.zero, Vector3.one);
            }

            // South Cathedral Exit Doorway (centered cleanly at x=0, z=-4)
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorFrameStone_01.prefab", env, new Vector3(0f, 0f, -4f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorPlank_01.prefab", env, new Vector3(0f, 0f, -4f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_StairHalfFlight_01.prefab", env, new Vector3(0f, 0f, -3.3f), Vector3.zero, new Vector3(0.8f, 0.45f, 0.55f));
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_SconceIronLit_01.prefab", env, new Vector3(-2.2f, 2.2f, -3.7f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_SconceIronLit_01.prefab", env, new Vector3(2.2f, 2.2f, -3.7f), Vector3.zero, Vector3.one);

            // Upper Belfry Bell & Rafters Platform (Y = 4.4, x = -8..8, z = 2..12)
            CreateWall("UpperPlatformCollider", new Vector3(0f, 4.2f, 7f), new Vector3(18f, 0.4f, 10f), null, env);
            for (int gx = -8; gx <= 8; gx += 4)
            for (int gz = 4; gz <= 10; gz += 4)
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_CeilTimberBoarded_01.prefab", env, new Vector3(gx, 4.4f, gz), Vector3.zero, Vector3.one);

            // Heavy timber support columns & balcony railing (open gap at x = 3.5..6.5 for ladder arrival)
            foreach (var col in new[] { new Vector3(-7.0f, 0f, 2.4f), new Vector3(7.0f, 0f, 2.4f), new Vector3(-7.0f, 0f, 9.5f), new Vector3(7.0f, 0f, 9.5f) })
                PlaceProp(Mfp + "Columns/wood_column.001.prefab", env, col, Vector3.zero, new Vector3(1.1f, 1.55f, 1.1f));
            foreach (float fx in new[] { -6.5f, -4.0f, -1.5f, 1.0f })
                PlaceProp(Mfp + "Fences/Small/fence.001.prefab", env, new Vector3(fx, 4.4f, 2.2f), Vector3.zero, Vector3.one);

            // Collapsed stone stairway (stops below the upper deck) + Rubble heap
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_StairHalfFlight_01.prefab", env, new Vector3(-6.8f, 0f, 0.2f), new Vector3(0f, 90f, -8f), Vector3.one);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_RubbleHeapMound_01.prefab", env, new Vector3(-5.2f, 0f, 1.0f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_RubbleHeapAgainst_01.prefab", env, new Vector3(-7.2f, 0f, 1.4f), new Vector3(0f, 90f, 0f), Vector3.one);

            // Maintenance Ladder bypass (aligned with the open railing gap at x = 4.5, z = 2.0)
            var upperLanding = new GameObject("UpperBelfryLanding").transform;
            upperLanding.SetParent(env, false);
            upperLanding.position = new Vector3(4.5f, 5.4f, 3.4f);

            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_LadderTimber_01.prefab", env, new Vector3(4.5f, 0f, 1.85f), Vector3.zero, new Vector3(1.1f, 1.38f, 1.1f));
            var ladderTrigger = new GameObject("MaintenanceLadder");
            ladderTrigger.layer = InteractableLayer;
            ladderTrigger.transform.SetParent(env, false);
            ladderTrigger.transform.position = new Vector3(4.5f, 1.8f, 1.8f);
            ladderTrigger.AddComponent<BoxCollider>().size = new Vector3(1.4f, 3.6f, 0.9f);
            var tp = ladderTrigger.AddComponent<PlayerTeleportPoint>();
            tp.destination = upperLanding;
            var ladderLock = ladderTrigger.AddComponent<LockableInteractable>();
            ladderLock.requiredItemId = "";
            ladderLock.lockedPromptAction = "[L-Click] Climb Maintenance Ladder";
            ladderLock.unlockedPromptAction = "[L-Click] Climb Maintenance Ladder";
            ladderLock.unlockedDescription = "Bypass the collapsed stairway via the maintenance ladder.";
            ladderLock.boardCaseId = 2;
            ladderLock.onInteractSuccess ??= new UnityEngine.Events.UnityEvent();
            UnityEventTools.AddPersistentListener(ladderLock.onInteractSuccess, tp.TeleportPlayer);

            // Return ladder at top of the opening
            var lowerLanding = new GameObject("LowerBelfryLanding").transform;
            lowerLanding.SetParent(env, false);
            lowerLanding.position = new Vector3(4.5f, 1.0f, 0.2f);
            var downLadder = new GameObject("UpperReturnLadder");
            downLadder.layer = InteractableLayer;
            downLadder.transform.SetParent(env, false);
            downLadder.transform.position = new Vector3(4.5f, 4.9f, 2.0f);
            downLadder.AddComponent<BoxCollider>().size = new Vector3(1.4f, 1.5f, 0.9f);
            var tpDown = downLadder.AddComponent<PlayerTeleportPoint>();
            tpDown.destination = lowerLanding;
            var downLock = downLadder.AddComponent<LockableInteractable>();
            downLock.requiredItemId = "";
            downLock.unlockedPromptAction = "[L-Click] Descend Maintenance Ladder";
            downLock.unlockedDescription = "Climb down to the lower belfry landing.";
            downLock.boardCaseId = 2;
            downLock.onInteractSuccess ??= new UnityEngine.Events.UnityEvent();
            UnityEventTools.AddPersistentListener(downLock.onInteractSuccess, tpDown.TeleportPlayer);

            // Lower & Upper Cathedral Belfry Decor
            var decor = new GameObject("BelfryDecor").transform;
            decor.SetParent(env, false);

            // Central runner carpet & wall banners
            PlaceProp(Dfk + "Decoration/Textiles/dfk_carpet_black_small.prefab", decor, new Vector3(0f, 0.02f, 0.5f), Vector3.zero, new Vector3(1.2f, 1f, 2.2f));
            PlaceProp(Dfk + "Decoration/Flags/dfk_flag_02_a.prefab", decor, new Vector3(-9.7f, 2.4f, 0f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Dfk + "Decoration/Flags/dfk_flag_02_a.prefab", decor, new Vector3(-9.7f, 2.4f, 8f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Dfk + "Decoration/Flags/dfk_flag_02_a.prefab", decor, new Vector3(9.7f, 2.4f, 0f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Dfk + "Decoration/Flags/dfk_flag_02_a.prefab", decor, new Vector3(9.7f, 2.4f, 8f), new Vector3(0f, -90f, 0f), Vector3.one);

            // Lower floor: pews, bell-ringer's desk, braziers, cupboards, crates, rope coils & debris
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_BenchBacked_01.prefab", decor, new Vector3(3.4f, 0f, -1.6f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_BenchBacked_01.prefab", decor, new Vector3(3.4f, 0f, 0.2f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_BrazierTripodLit_01.prefab", decor, new Vector3(-2.6f, 0f, -1.8f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_BrazierTripodLit_01.prefab", decor, new Vector3(2.6f, 0f, -1.8f), Vector3.zero, Vector3.one);

            // SE corner: Accord's bell-ringer desk & supply cupboards
            PlaceProp(Dfk + "Furnitures/dfk_desk_02.prefab", decor, new Vector3(7.4f, 0f, -1.5f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_ChairPlain_01.prefab", decor, new Vector3(6.3f, 0f, -1.5f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_OilLampBrass_01.prefab", decor, new Vector3(7.4f, 0.85f, -1.2f), Vector3.zero, Vector3.one);
            PlaceProp(Mfp + "Miscellaneous/scrolls.prefab", decor, new Vector3(7.3f, 0.85f, -1.9f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_CupboardClosed_01.prefab", decor, new Vector3(9.0f, 0f, 2.2f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_ShelfPlank_01.prefab", decor, new Vector3(9.0f, 0f, 4.6f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_CrateBanded_01.prefab", decor, new Vector3(7.8f, 0f, 6.2f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_CrateMedium_01.prefab", decor, new Vector3(7.8f, 0.9f, 6.2f), new Vector3(0f, 18f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_BarrelUpright_01.prefab", decor, new Vector3(8.2f, 0f, 7.8f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_BarrelUpright_01.prefab", decor, new Vector3(8.2f, 0f, 9.0f), new Vector3(0f, 30f, 0f), Vector3.one);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_RopeDescentCoil_01.prefab", decor, new Vector3(6.2f, 0f, 2.8f), Vector3.zero, Vector3.one);

            // Lower North & West: collapsed stair rubble, chests, urns, lanterns
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_DebrisScatter_01.prefab", decor, new Vector3(-5.0f, 0.02f, 2.6f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_LanternFloor_01.prefab", decor, new Vector3(-3.3f, 0f, 5.4f), Vector3.zero, Vector3.one);
            PlaceProp(Dfk + "Containers/Chests/dfk_chest_02_closed.prefab", decor, new Vector3(-8.4f, 0f, 9.5f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_UrnUrn_01.prefab", decor, new Vector3(-2.5f, 0f, 10.6f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_UrnUrn_01.prefab", decor, new Vector3(2.5f, 0f, 10.6f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_PotClay_01.prefab", decor, new Vector3(0f, 0f, 10.8f), Vector3.zero, Vector3.one);

            // Upper Bell & Rafters Platform (y = 4.4): scaffolding, maintenance bench, cressets, rope coils, rafters
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_ShoringProps_01.prefab", decor, new Vector3(-5.8f, 4.4f, 8.2f), new Vector3(0f, 35f, 0f), Vector3.one);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_ShoringProps_01.prefab", decor, new Vector3(6.4f, 4.4f, 8.6f), new Vector3(0f, -35f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_CrateMedium_01.prefab", decor, new Vector3(-4.4f, 4.4f, 8.8f), new Vector3(0f, 12f, 0f), Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_CandleTriple_01.prefab", decor, new Vector3(-4.6f, 5.2f, 8.5f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_TableTrestle_01.prefab", decor, new Vector3(5.6f, 4.4f, 6.5f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_LanternHooded_01.prefab", decor, new Vector3(5.5f, 5.18f, 6.0f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_BucketIron_01.prefab", decor, new Vector3(5.7f, 5.18f, 7.0f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_CrateLarge_01.prefab", decor, new Vector3(-7.4f, 4.4f, 4.2f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_SackUpright_01.prefab", decor, new Vector3(-7.0f, 4.4f, 5.5f), new Vector3(0f, 25f, 0f), Vector3.one);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_RopeDescentCoil_01.prefab", decor, new Vector3(-2.6f, 4.4f, 6.2f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_CressetLit_01.prefab", decor, new Vector3(-2.6f, 4.4f, 4.8f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_CressetLit_01.prefab", decor, new Vector3(2.6f, 4.4f, 4.8f), Vector3.zero, Vector3.one);

            AddPointLight(env, new Vector3(0f, 3.2f, 2.0f), new Color(0.75f, 0.84f, 1f), 2.8f, 14f);
            AddPointLight(env, new Vector3(0f, 7.2f, 6.5f), new Color(0.85f, 0.78f, 1f), 3.0f, 15f);

            var clues = new GameObject("Clues").transform;
            SceneManager.MoveGameObjectToScene(clues.gameObject, scene);

            // C1_02: Fractured Resonance Crystal (lower floor)
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_PillarBroken_01.prefab", env, new Vector3(-4.5f, 0f, 6.5f), Vector3.zero, new Vector3(0.7f, 0.45f, 0.7f));
            var crystal = PlaceProp(Dfk + "Lightsources/dfk_crystal_01_blue.prefab", clues, new Vector3(-4.5f, 0.85f, 6.5f), new Vector3(12f, 20f, -10f), new Vector3(1.3f, 1.3f, 1.3f));
            AddPointLight(env, new Vector3(-4.5f, 1.6f, 6.5f), new Color(0.35f, 0.78f, 1f), 2.2f, 7f);
            CreateClueNode("C1_FracturedCrystal", "C1_02", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Shattered acoustic focus crystal beneath the Great Bell. Radial shear fractures prove internal harmonic overload.",
                new Vector3(-4.5f, 1.1f, 6.5f), new Vector3(1.5f, 1.6f, 1.5f), clues, crystal);

            // C2_02: Dark Mana Dust on Clapper (upper platform Great Bell in timber belfry frame) — requires GolemEye
            var bellFrame = new GameObject("GreatBellHousing").transform;
            bellFrame.SetParent(env, false);
            PlaceProp(Mfp + "Columns/wood_column.001.prefab", bellFrame, new Vector3(-1.8f, 4.4f, 6.8f), Vector3.zero, new Vector3(1f, 1.25f, 1f));
            PlaceProp(Mfp + "Columns/wood_column.001.prefab", bellFrame, new Vector3(1.8f, 4.4f, 6.8f), Vector3.zero, new Vector3(1f, 1.25f, 1f));
            CreatePrimitiveMarker("BellCrossbeam", PrimitiveType.Cube, new Vector3(0f, 7.65f, 6.8f),
                new Vector3(4.2f, 0.3f, 0.35f), new Color(0.28f, 0.19f, 0.12f), bellFrame, false);

            var bellRoot = new GameObject("GreatBell").transform;
            bellRoot.SetParent(clues, false);
            bellRoot.position = new Vector3(0f, 6.2f, 6.8f);
            CreatePrimitiveMarker("BellCrown", PrimitiveType.Sphere, new Vector3(0f, 7.05f, 6.8f),
                new Vector3(1.35f, 0.9f, 1.35f), new Color(0.64f, 0.49f, 0.22f), bellRoot, false);
            CreatePrimitiveMarker("BellBody", PrimitiveType.Cylinder, new Vector3(0f, 6.45f, 6.8f),
                new Vector3(1.45f, 0.55f, 1.45f), new Color(0.60f, 0.46f, 0.20f), bellRoot, false);
            CreatePrimitiveMarker("BellRim", PrimitiveType.Cylinder, new Vector3(0f, 5.95f, 6.8f),
                new Vector3(1.85f, 0.18f, 1.85f), new Color(0.68f, 0.53f, 0.24f), bellRoot, false);
            var clapper = CreatePrimitiveMarker("BellClapper", PrimitiveType.Sphere, new Vector3(0f, 5.55f, 6.8f),
                new Vector3(0.42f, 0.42f, 0.42f), new Color(0.22f, 0.14f, 0.32f), bellRoot, false);
            AddAmbientMist(clapper.transform, new Color(0.48f, 0.20f, 0.75f, 0.45f));
            AddPointLight(env, new Vector3(0f, 5.6f, 6.8f), new Color(0.58f, 0.28f, 0.92f), 2.2f, 6f);
            CreateClueNode("C2_BellClapperDust", "C2_02", ClueNode.Kind.Physical, ClueNode.Tool.GolemEye, ClueNode.State.Hidden,
                "Black metallic soot on the bell clapper's strike face. Spectral scan reads an unnatural acoustic muting field.",
                new Vector3(0f, 5.8f, 6.8f), new Vector3(2.0f, 2.0f, 2.0f), clues, bellRoot.gameObject);

            // C3_02: Blackmailer's Coercion Note (on crate behind timber shoring)
            var note = PlaceProp(Mfp + "Miscellaneous/scroll.prefab", clues, new Vector3(-4.2f, 5.22f, 8.9f), new Vector3(0f, 25f, 0f), new Vector3(1.4f, 1.4f, 1.4f));
            CreateClueNode("C3_CoercionNote", "C3_02", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Crumpled letter addressed to Accord, sealed with the Veiled Circle's veil sigil. Threatens his sister Marian's asylum.",
                new Vector3(-4.2f, 5.35f, 8.9f), new Vector3(1.4f, 1.2f, 1.4f), clues, note);

            // L2_02: Rear foundation beam (upper rear) — requires AetherSight
            AddPointLight(env, new Vector3(0f, 5.2f, 11.2f), new Color(0.25f, 0.85f, 0.95f), 1.4f, 5f);
            CreateClueNode("L2_RunicLog2_Beam", "L2_02", ClueNode.Kind.Physical, ClueNode.Tool.AetherSight, ClueNode.State.Hidden,
                "Ancient harmonic rune etched into the rear masonry beam: Acoustic Stabilization equations (Runic Log #2).",
                new Vector3(0f, 5.2f, 11.3f), new Vector3(2.0f, 1.8f, 1.2f), clues);

            // Exit stairway down to Cathedral Courtyard / Nave
            CreateExitPortal("Exit_BelfryStair", 2, "Hub_CathedralCourtyard",
                "[L-Click] Descend to Cathedral",
                "Return down the belfry stairs to the Cathedral of Saint Althea.",
                new Vector3(0f, 1.2f, -3.4f), new Vector3(2.2f, 2.4f, 1.6f), clues);

            // Dynamic Active Goal controller for Case 2
            var ctrlGo = new GameObject("Case2ObjectiveController");
            SceneManager.MoveGameObjectToScene(ctrlGo, scene);
            var objCtrl = ctrlGo.AddComponent<InvestigationObjectiveController>();
            objCtrl.caseId = 2;
            objCtrl.requiredClueIds = new[] { "C1_02", "C2_02", "C3_02" };
            objCtrl.gatherObjectiveFormat = "Inspect chime crystal, climb ladder to Great Bell [Tab], and search rafters ({0}/{1}).";
            objCtrl.boardObjective = "Evidence gathered. Open the Deduction Board [H] or descend to the Cathedral.";
            objCtrl.solvedObjective = "Board solved! Descend the belfry stairs to confront Accord.";

            SaveScene(scene, Case2ScenePath);
        }

        [MenuItem("Morsoul/3D/Build Case 3 - Whisperwood Grove")]
        public static void BuildCase3Whisperwood()
        {
            var scene = CreateBaseScene(
                Case3ScenePath,
                new Color(0.15f, 0.24f, 0.18f),
                new Color(0.11f, 0.18f, 0.14f),
                0.025f,
                new Vector3(0f, 1.0f, 0f),
                Vector3.zero);

            var env = new GameObject("Environment").transform;
            SceneManager.MoveGameObjectToScene(env.gameObject, scene);

            // Forest clearing ground + invisible perimeter walls
            EnsureSolidBox("ForestGround", new Vector3(0f, -0.25f, 8f), new Vector3(32f, 0.5f, 32f), new Color(0.16f, 0.24f, 0.14f), env);
            BuildPerimeterColliders(env, new Vector3(0f, 0f, 8f), new Vector3(30f, 6f, 30f));

            // Ring of trees & bushes
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16f;
                Vector3 p = new Vector3(Mathf.Cos(angle) * 13f, 0f, 8f + Mathf.Sin(angle) * 13f);
                PlaceProp(Mfp + "Miscellaneous/tree.prefab", env, p, new Vector3(0f, i * 23f, 0f), Vector3.one * 1.4f);
                PlaceProp(Bushes + $"P_Bush0{(i % 5) + 1}.prefab", env, p * 0.88f, Vector3.zero, Vector3.one);
            }

            // Elder Spirit Tree at (0, 0, 14) + Ritual Stone Altar at (-4, 0, 8)
            PlaceProp(Mfp + "Miscellaneous/tree.prefab", env, new Vector3(0f, 0f, 14f), Vector3.zero, Vector3.one * 2.4f);
            PlaceProp(Mfp + "Miscellaneous/granite_panel.prefab", env, new Vector3(-4f, 0.5f, 8f), Vector3.zero, new Vector3(1.8f, 1.0f, 1.2f));
            AddPointLight(env, new Vector3(0f, 4f, 11f), new Color(0.45f, 0.95f, 0.65f), 2.5f, 18f);

            var clues = new GameObject("Clues").transform;
            SceneManager.MoveGameObjectToScene(clues.gameObject, scene);

            // C1_03: Brass Extraction Siphon at Elder Tree roots
            var siphon = CreatePrimitiveMarker("BrassExtractionSiphon", PrimitiveType.Cylinder, new Vector3(0f, 0.8f, 12.2f),
                new Vector3(0.55f, 1.2f, 0.55f), new Color(0.75f, 0.55f, 0.2f), clues, false);
            CreateClueNode("C1_ExtractionSiphon", "C1_03", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Industrial pneumatic tap driven deep into the Elder Spirit Tree's taproot, draining raw mana into pressurized canisters.",
                new Vector3(0f, 1.0f, 12.2f), new Vector3(1.5f, 1.8f, 1.5f), clues, siphon);

            // C2_03: Inscribed Silver Ritual Dagger on Altar
            var dagger = CreatePrimitiveMarker("SilverRitualDagger", PrimitiveType.Cube, new Vector3(-4f, 1.15f, 7.6f),
                new Vector3(0.18f, 0.08f, 0.65f), new Color(0.82f, 0.86f, 0.92f), clues, false);
            CreateClueNode("C2_RitualDagger", "C2_03", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Ceremonial silver dagger bearing the Veiled Circle's eclipsed eye insignia, coated with Case 1's solvent stabilizer.",
                new Vector3(-4f, 1.2f, 7.6f), new Vector3(1.2f, 1.0f, 1.2f), clues, dagger);

            // C3_03: Vane's Discarded Ritual Cloak
            var cloak = PlaceProp(Mfp + "Miscellaneous/carpet.prefab", clues, new Vector3(-4.4f, 0.05f, 9.5f), new Vector3(0f, 35f, 0f), new Vector3(0.8f, 0.8f, 0.8f));
            CreateClueNode("C3_VaneCloak", "C3_03", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Vane's discarded ritual cloak. Lingers with fanatical zeal and deceit tying him to the Sanctum Core ritual.",
                new Vector3(-4.4f, 0.6f, 9.5f), new Vector3(1.6f, 1.2f, 1.6f), clues, cloak);

            // L3_03: Central Altar Ground — requires AetherSight
            CreateClueNode("L3_RunicLog3_AltarGround", "L3_03", ClueNode.Kind.Physical, ClueNode.Tool.AetherSight, ClueNode.State.Hidden,
                "Buried pre-cataclysm telemetry beneath the altar: Displacement Device Telemetry (Runic Log #3).",
                new Vector3(0f, 0.6f, 8f), new Vector3(1.8f, 1.2f, 1.8f), clues);

            // Exit Boundary Stone back to Whisperwood Trail
            CreateExitPortal("Exit_GroveBoundaryStone", 3, "Hub_Wilderness",
                "[L-Click] Return to Whisperwood Trail",
                "Exit the deep grove clearing back to the Whisperwood Ranger Outpost.",
                new Vector3(0f, 1.2f, -3.5f), new Vector3(2.2f, 2.4f, 1.4f), clues);

            SaveScene(scene, Case3ScenePath);
        }

        [MenuItem("Morsoul/3D/Build Case 4 - Iron Hollow Sub-Facility")]
        public static void BuildCase4IronHollow()
        {
            var scene = CreateBaseScene(
                Case4ScenePath,
                new Color(0.16f, 0.13f, 0.11f),
                new Color(0.10f, 0.08f, 0.07f),
                0.025f,
                new Vector3(0f, 1.0f, 0f),
                Vector3.zero);

            var env = new GameObject("Environment").transform;
            SceneManager.MoveGameObjectToScene(env.gameObject, scene);

            // Subterranean Dwarven facility (22x26)
            BuildBoxRoom(env, new Vector3(0f, 0f, 10f), new Vector3(22f, 7f, 26f),
                new Color(0.20f, 0.18f, 0.17f), new Color(0.25f, 0.22f, 0.20f), true);

            PlaceProp(Mfp + "Miscellaneous/furnace.prefab", env, new Vector3(-7f, 0f, 5f), Vector3.zero, Vector3.one);
            PlaceProp(Mfp + "Miscellaneous/anvil.prefab", env, new Vector3(6f, 0f, 4f), Vector3.zero, Vector3.one);
            PlaceProp(Mfp + "Platforms/platform.prefab", env, new Vector3(0f, 0f, 0f), Vector3.zero, Vector3.one);
            PlaceProp(Mfp + "Castle walls/stone_half_wall.prefab", env, new Vector3(-5f, 0f, 12f), Vector3.zero, Vector3.one);
            PlaceProp(Mfp + "Castle walls/stone_half_wall.prefab", env, new Vector3(5f, 0f, 12f), Vector3.zero, Vector3.one);
            AddPointLight(env, new Vector3(0f, 5f, 6f), new Color(1f, 0.68f, 0.42f), 2.8f, 18f);
            AddPointLight(env, new Vector3(0f, 5f, 17f), new Color(0.45f, 0.85f, 1f), 2.5f, 16f);

            var clues = new GameObject("Clues").transform;
            SceneManager.MoveGameObjectToScene(clues.gameObject, scene);

            // C1_04: Tampered Pneumatic Valves (left steam line)
            var valve = CreatePrimitiveMarker("PneumaticValves", PrimitiveType.Cylinder, new Vector3(-4.5f, 1.1f, 6f),
                new Vector3(0.8f, 0.8f, 0.8f), new Color(0.58f, 0.36f, 0.22f), clues, false);
            CreateClueNode("C1_PneumaticValves", "C1_04", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "High-pressure pneumatic release valves jammed with iron wedge pins to force catastrophic backflow.",
                new Vector3(-4.5f, 1.1f, 6f), new Vector3(1.5f, 1.5f, 1.5f), clues, valve);

            // C2_04: Chemically Dissolved Bypass Conduits (center junction) — requires GolemEye
            var conduit = CreatePrimitiveMarker("DissolvedConduits", PrimitiveType.Cube, new Vector3(0f, 0.6f, 8f),
                new Vector3(1.6f, 0.5f, 0.8f), new Color(0.45f, 0.25f, 0.65f), clues, false);
            CreateClueNode("C2_DissolvedConduits", "C2_04", ClueNode.Kind.Physical, ClueNode.Tool.GolemEye, ClueNode.State.Hidden,
                "Copper control wiring corroded into slag. Golem Eye confirms the same synthetic mana solvent from Case 1 and Case 3.",
                new Vector3(0f, 0.8f, 8f), new Vector3(1.8f, 1.2f, 1.4f), clues, conduit);

            // C3_04: Pre-Cataclysm Sub-Lab Blast Door (midway partition at z=12)
            var blastDoor = PlaceProp(Dfk + "Doors and Gates/dfk_door_01.prefab", clues, new Vector3(0f, 0f, 12f), Vector3.zero, new Vector3(1.4f, 1.4f, 1.4f));
            CreateClueNode("C3_SubLabBlastDoor", "C3_04", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Massive pre-cataclysm titanium blast door tampered with from outside to force a stasis lockdown over the engineering bay.",
                new Vector3(0f, 1.2f, 11.7f), new Vector3(2.2f, 2.4f, 1.2f), clues, blastDoor);

            // L4_04: Ancient Primary Terminal inside sub-lab (z=18) — requires Both (AetherSight + GolemEye)
            var terminal = PlaceProp(Dfk + "Furnitures/dfk_desk_02.prefab", clues, new Vector3(0f, 0f, 18f), Vector3.zero, Vector3.one);
            CreateClueNode("L4_RunicLog4_Terminal", "L4_04", ClueNode.Kind.Physical, ClueNode.Tool.Both, ClueNode.State.Hidden,
                "Decrypted pre-cataclysm primary terminal: Planetary Stabilizer Console Blueprints (Runic Log #4).",
                new Vector3(0f, 1.1f, 18f), new Vector3(1.8f, 1.6f, 1.4f), clues, terminal);

            // Exit Shaft Elevator back to Iron Hollow surface
            CreateExitPortal("Exit_ShaftElevator", 4, "Hub_IronHollow",
                "[L-Click] Ascend Shaft Elevator",
                "Ride the deep-shaft elevator back to the Iron Hollow surface.",
                new Vector3(0f, 1.2f, -2.5f), new Vector3(2.2f, 2.4f, 1.4f), clues);

            SaveScene(scene, Case4ScenePath);
        }

        [MenuItem("Morsoul/3D/Build Case 5 - Sanctum Core Staging")]
        public static void BuildCase5Sanctum()
        {
            var scene = CreateBaseScene(
                Case5ScenePath,
                new Color(0.14f, 0.08f, 0.22f),
                new Color(0.10f, 0.05f, 0.16f),
                0.02f,
                new Vector3(0f, 1.0f, 0f),
                Vector3.zero);

            var env = new GameObject("Environment").transform;
            SceneManager.MoveGameObjectToScene(env.gameObject, scene);

            // Grand Cataclysmic Chamber (cutscene staging per Case_05_Sanctum.md)
            BuildBoxRoom(env, new Vector3(0f, 0f, 12f), new Vector3(28f, 12f, 30f),
                new Color(0.12f, 0.09f, 0.18f), new Color(0.18f, 0.14f, 0.26f), true);

            // North & South Siphon Pylons
            PlaceProp(Mfp + "Columns/Stone/stone_column.prefab", env, new Vector3(-6f, 0f, 12f), Vector3.zero, new Vector3(1.5f, 1.8f, 1.5f));
            PlaceProp(Mfp + "Columns/Stone/stone_column.prefab", env, new Vector3(6f, 0f, 12f), Vector3.zero, new Vector3(1.5f, 1.8f, 1.5f));
            // Ritual Dais & Primary Mana Fissure & Stabilizer Console anchor
            EnsureSolidBox("RitualDais", new Vector3(0f, 0.4f, 12f), new Vector3(8f, 0.8f, 8f), new Color(0.22f, 0.16f, 0.30f), env);
            PlaceProp(Dfk + "Lightsources/dfk_crystal_01_blue.prefab", env, new Vector3(0f, 1.0f, 18f), Vector3.zero, new Vector3(3f, 3f, 3f));
            PlaceProp(Dfk + "Furnitures/dfk_desk_02.prefab", env, new Vector3(0f, 0f, 22f), Vector3.zero, Vector3.one);
            AddPointLight(env, new Vector3(0f, 6f, 15f), new Color(0.65f, 0.35f, 1f), 4.0f, 28f);

            SaveScene(scene, Case5ScenePath);
        }

        // --- Helpers ---------------------------------------------------------

        static Scene CreateBaseScene(string scenePath, Color ambient, Color fogColor, float fogDensity,
            Vector3 spawnPos, Vector3 spawnEuler)
        {
            var dir = System.IO.Path.GetDirectoryName(scenePath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = true;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;

            var sharedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharedRootPath);
            if (sharedPrefab != null)
                PrefabUtility.InstantiatePrefab(sharedPrefab, scene);
            else
                Debug.LogWarning($"InvestigationLevelsBuilder: missing SharedRoot at '{SharedRootPath}'.");

            var spawn = new GameObject("PlayerSpawn");
            spawn.tag = "PlayerSpawn";
            spawn.transform.position = spawnPos;
            spawn.transform.eulerAngles = spawnEuler;
            SceneManager.MoveGameObjectToScene(spawn, scene);

            return scene;
        }

        static void SaveScene(Scene scene, string scenePath)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        static GameObject PlaceProp(string prefabPath, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject go;
            if (prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                Debug.LogWarning($"InvestigationLevelsBuilder: prefab not found '{prefabPath}', using placeholder.");
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = System.IO.Path.GetFileNameWithoutExtension(prefabPath);
            }
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.eulerAngles = euler;
            go.transform.localScale = scale;
            return go;
        }

        static void TileFloorProps(Transform parent, string floorPrefabPath, float minX, float maxX, float minZ, float maxZ, float step)
        {
            for (float x = minX; x <= maxX; x += step)
            for (float z = minZ; z <= maxZ; z += step)
                PlaceProp(floorPrefabPath, parent, new Vector3(x, 0.01f, z), Vector3.zero, Vector3.one);
        }

        static void BuildBoxRoom(Transform parent, Vector3 center, Vector3 size, Color floorColor, Color wallColor, bool hasCeiling)
        {
            EnsureSolidBox("Floor", new Vector3(center.x, -0.25f, center.z), new Vector3(size.x, 0.5f, size.z), floorColor, parent);
            if (hasCeiling)
                EnsureSolidBox("Ceiling", new Vector3(center.x, size.y, center.z), new Vector3(size.x, 0.5f, size.z), wallColor, parent);
            BuildPerimeterColliders(parent, center, size, wallColor);
        }

        static void BuildPerimeterColliders(Transform parent, Vector3 center, Vector3 size, Color? visibleWallColor = null)
        {
            float halfX = size.x * 0.5f;
            float halfZ = size.z * 0.5f;
            float h = size.y;
            float y = h * 0.5f;
            CreateWall("Wall_North", new Vector3(center.x, y, center.z + halfZ), new Vector3(size.x, h, 0.5f), visibleWallColor, parent);
            CreateWall("Wall_South", new Vector3(center.x, y, center.z - halfZ), new Vector3(size.x, h, 0.5f), visibleWallColor, parent);
            CreateWall("Wall_East", new Vector3(center.x + halfX, y, center.z), new Vector3(0.5f, h, size.z), visibleWallColor, parent);
            CreateWall("Wall_West", new Vector3(center.x - halfX, y, center.z), new Vector3(0.5f, h, size.z), visibleWallColor, parent);
        }

        static void CreateWall(string name, Vector3 pos, Vector3 scale, Color? color, Transform parent)
        {
            if (color.HasValue)
            {
                EnsureSolidBox(name, pos, scale, color.Value, parent);
                return;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = scale;
        }

        static GameObject EnsureSolidBox(string name, Vector3 pos, Vector3 scale, Color color, Transform parent)
        {
            return CreatePrimitiveMarker(name, PrimitiveType.Cube, pos, scale, color, parent, true);
        }

        static GameObject CreatePrimitiveMarker(string name, PrimitiveType type, Vector3 pos, Vector3 scale,
            Color color, Transform parent, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(shader) { color = color };
                mr.sharedMaterial = mat;
            }
            return go;
        }

        static void CreateClueNode(string name, string clueId, ClueNode.Kind kind, ClueNode.Tool tool,
            ClueNode.State state, string description, Vector3 pos, Vector3 colliderSize, Transform parent,
            GameObject visualChild = null)
        {
            var go = new GameObject(name);
            go.layer = InteractableLayer;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var bc = go.AddComponent<BoxCollider>();
            bc.size = colliderSize;

            var clue = go.AddComponent<ClueNode>();
            clue.id = clueId;
            clue.kind = kind;
            clue.requiredTool = tool;
            clue.state = state;
            clue.defaultDescription = description;

            var psGo = new GameObject("ScanParticles");
            psGo.transform.SetParent(go.transform, false);
            var ps = psGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.startLifetime = 0.6f;
            main.startSpeed = 0.5f;
            main.startSize = 0.12f;
            main.startColor = new Color(0.4f, 0.9f, 1f, 0.9f);
            clue.scanParticles = ps;

            if (visualChild == null)
                CreatePrimitiveMarker("Marker", PrimitiveType.Sphere, pos, Vector3.one * 0.25f, new Color(0.4f, 0.85f, 1f), go.transform, false);
        }

        static void CreateExitPortal(string name, int caseId, string fallbackHub, string promptAction,
            string description, Vector3 pos, Vector3 size, Transform parent)
        {
            var go = new GameObject(name);
            go.layer = InteractableLayer;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;

            var exit = go.AddComponent<InvestigationExitTrigger>();
            exit.caseId = caseId;
            exit.fallbackHubScene = fallbackHub;

            var lockable = go.AddComponent<LockableInteractable>();
            lockable.requiredItemId = "";
            lockable.requireBoardSolved = false;
            lockable.boardCaseId = caseId;
            lockable.lockedPromptAction = promptAction;
            lockable.unlockedPromptAction = promptAction;
            lockable.unlockedDescription = description;
            lockable.onInteractSuccess ??= new UnityEngine.Events.UnityEvent();
            UnityEventTools.AddPersistentListener(lockable.onInteractSuccess, exit.ExitToHub);
        }

        static void AddPointLight(Transform parent, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject("PointLight");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
        }

        static void AddAmbientMist(Transform parent, Color color)
        {
            var go = new GameObject("AmbientMist");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 2.2f;
            main.startSpeed = 0.25f;
            main.startSize = 0.35f;
            main.startColor = color;
            main.maxParticles = 40;
            var emission = ps.emission;
            emission.rateOverTime = 12f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.7f;
        }

        static void RegisterBuildScenes(params string[] scenePaths)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var path in scenePaths)
            {
                bool exists = false;
                foreach (var s in list)
                {
                    if (s.path == path) { s.enabled = true; exists = true; break; }
                }
                if (!exists)
                    list.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
