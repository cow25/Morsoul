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

            // Invisible collision bounds (20x28 covering Room 1 z=-6..6 and Room 2 z=6..22) so player never clips out
            CreateWall("FloorCollider", new Vector3(0f, -0.25f, 8f), new Vector3(20f, 0.5f, 28f), null, env);
            CreateWall("CeilingCollider", new Vector3(0f, 4.3f, 8f), new Vector3(20f, 0.5f, 28f), null, env);
            BuildPerimeterColliders(env, new Vector3(0f, 0f, 8f), new Vector3(20f, 4.5f, 28f));
            CreateWall("DividerWall_W", new Vector3(-5.6f, 2.25f, 6f), new Vector3(8.8f, 4.5f, 0.6f), null, env);
            CreateWall("DividerWall_E", new Vector3(5.6f, 2.25f, 6f), new Vector3(8.8f, 4.5f, 0.6f), null, env);

            // 4m x 4m LowPolyDungeonFree stone floor tiles (-8..8 in X, -4..20 in Z)
            for (int gx = -8; gx <= 8; gx += 4)
            for (int gz = -4; gz <= 20; gz += 4)
            {
                string tile = Lpd + "02_Floors/SM_LPD_Floor_PavingFlagstone_01.prefab";
                if (gx == 4 && gz == 4) tile = Lpd + "02_Floors/SM_LPD_Floor_PavingDrain_01.prefab";
                else if (gx == 0 && gz == 12) tile = Lpd + "02_Floors/SM_LPD_Floor_PavingHatchLidded_01.prefab";
                else if (gx == 4 && gz == 8) tile = Lpd + "02_Floors/SM_LPD_Floor_PavingChannelStraight_01.prefab";
                else if ((gx + gz) % 8 == 0) tile = Lpd + "02_Floors/SM_LPD_Floor_PavingFlagstoneWorn_01.prefab";
                PlaceProp(tile, env, new Vector3(gx, 0f, gz), Vector3.zero, Vector3.one);
            }
            // Sub-floor backing beneath the lidded hatch tile so no void/sky shows through
            PlaceProp(Lpd + "02_Floors/SM_LPD_Floor_PavingFlagstone_01.prefab", env, new Vector3(0f, -0.35f, 12f), Vector3.zero, Vector3.one);

            // Flat stone roof at Y = 4.0 across both rooms
            for (int gx = -8; gx <= 8; gx += 4)
            for (int gz = -4; gz <= 20; gz += 4)
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_CeilFlat_01.prefab", env, new Vector3(gx, 4.0f, gz), Vector3.zero, Vector3.one);

            // Sealed 4m x 4m perimeter stone walls (x = +/-10, z = -6 / +22) + dividing vault wall at z = 6
            for (int gx = -8; gx <= 8; gx += 4)
            {
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallAshlar_01.prefab", env, new Vector3(gx, 0f, 22f), new Vector3(0f, 180f, 0f), Vector3.one);
                string dividerWall = gx == 0 ? "01_Structure/SM_LPD_Struct_WallDoorway_01.prefab" : "01_Structure/SM_LPD_Struct_WallAshlar_01.prefab";
                PlaceProp(Lpd + dividerWall, env, new Vector3(gx, 0f, 6f), new Vector3(0f, 180f, 0f), Vector3.one);
                string southWall = gx == 0 ? "01_Structure/SM_LPD_Struct_WallDoorway_01.prefab" : "01_Structure/SM_LPD_Struct_WallCoursed_01.prefab";
                PlaceProp(Lpd + southWall, env, new Vector3(gx, 0f, -6f), Vector3.zero, Vector3.one);
            }
            for (int gz = -4; gz <= 20; gz += 4)
            {
                string wWall = gz == 0 ? "01_Structure/SM_LPD_Struct_WallNiche_01.prefab" : "01_Structure/SM_LPD_Struct_WallAshlar_01.prefab";
                PlaceProp(Lpd + wWall, env, new Vector3(-10f, 0f, gz), new Vector3(0f, 90f, 0f), Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallCoursed_01.prefab", env, new Vector3(10f, 0f, gz), new Vector3(0f, -90f, 0f), Vector3.one);
            }
            // Quoined corners
            foreach (var cPos in new[] { new Vector3(-10f, 0f, -6f), new Vector3(10f, 0f, -6f), new Vector3(-10f, 0f, 22f), new Vector3(10f, 0f, 22f) })
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_CornerPostQuoined_01.prefab", env, cPos, Vector3.zero, Vector3.one);

            // Structural ornate pillars framing Room 1 and Room 2
            foreach (var colPos in new[] { new Vector3(-4f, 0f, 0f), new Vector3(4f, 0f, 0f), new Vector3(-4f, 0f, 12f), new Vector3(4f, 0f, 12f), new Vector3(-4f, 0f, 18f), new Vector3(4f, 0f, 18f) })
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_PillarOrnate_01.prefab", env, colPos, Vector3.zero, Vector3.one);

            // South entry doorway + sealed double-leaf door + compact half-stair step (backed by solid stone)
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallAshlar_01.prefab", env, new Vector3(0f, 0f, -6.5f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorFrameStone_01.prefab", env, new Vector3(0f, 0f, -6f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorPlank_01.prefab", env, new Vector3(0f, 0f, -6f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorPlank_01.prefab", env, new Vector3(-0.9f, 0f, -6f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_StairHalfFlight_01.prefab", env, new Vector3(0f, 0f, -5.2f), Vector3.zero, new Vector3(0.8f, 0.5f, 0.6f));
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_SconceIronLit_01.prefab", env, new Vector3(-2.2f, 2.2f, -5.7f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_SconceIronLit_01.prefab", env, new Vector3(2.2f, 2.2f, -5.7f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_ChandelierRing_01.prefab", env, new Vector3(0f, 3.6f, 0f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_ChandelierRing_01.prefab", env, new Vector3(0f, 3.6f, 14f), Vector3.zero, Vector3.one);
            AddPointLight(env, new Vector3(0f, 3.2f, 0f), new Color(1f, 0.78f, 0.52f), 3.0f, 14f);
            AddPointLight(env, new Vector3(0f, 3.2f, 14f), new Color(0.85f, 0.88f, 1f), 2.4f, 16f);
            AddPointLight(env, new Vector3(0f, 2.6f, -4.5f), new Color(1f, 0.68f, 0.38f), 2.5f, 10f);

            // Room 2 (Inner Vault) emptied remnants — subtle evidence of stolen goods without clutter
            var storage = new GameObject("VaultStorage").transform;
            storage.SetParent(env, false);

            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_DunnagePallet_01.prefab", storage, new Vector3(-7.5f, 0f, 10.0f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_DunnagePallet_01.prefab", storage, new Vector3(-7.5f, 0f, 17.5f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_DunnageStillage_01.prefab", storage, new Vector3(7.6f, 0f, 10.0f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_ShelfPlank_01.prefab", storage, new Vector3(-6.8f, 0f, 21.0f), new Vector3(0f, 180f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_ShelfPlank_01.prefab", storage, new Vector3(6.8f, 0f, 21.0f), new Vector3(0f, 180f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_ChestFlat_01.prefab", storage, new Vector3(-5.0f, 0f, 20.5f), new Vector3(0f, 15f, 0f), Vector3.one);

            // Clues from Case_01_Vault.md
            var clues = new GameObject("Clues").transform;
            SceneManager.MoveGameObjectToScene(clues.gameObject, scene);

            // C1_01: Pristine Vault Lock (z = 6) — Open heavy vault door between Room 1 (Antechamber) and Room 2 (Inner Vault)
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorFrameStone_01.prefab", env, new Vector3(0f, 0f, 6f), new Vector3(0f, 180f, 0f), Vector3.one);
            var lockVisual = PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorStudded_01.prefab", clues, new Vector3(0.95f, 0f, 6f), new Vector3(0f, 255f, 0f), Vector3.one);
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorFittingPadlock_01.prefab", clues, new Vector3(0.75f, 1.15f, 6.7f), new Vector3(0f, 255f, 0f), Vector3.one * 1.4f);
            AddPointLight(env, new Vector3(0f, 2.8f, 5.2f), new Color(0.72f, 0.86f, 1f), 2.2f, 8f);
            CreateClueNode("C1_PristineVaultLock", "C1_01", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Heavy Dwarven multi-pin cylinder lock. Tumblers and pins are pristine with zero tool marks, shearing, or acid burns.",
                new Vector3(0.95f, 1.2f, 6.4f), new Vector3(1.1f, 2.2f, 1.2f), clues, lockVisual, useGroundEmbers: true);

            // C2_01: Vaporized Solvent Residue — Positioned directly beside the vault door clue
            var puddle = CreatePrimitiveMarker("SolventPuddle", PrimitiveType.Cylinder, new Vector3(2.2f, 0.04f, 5.1f),
                new Vector3(1.2f, 0.02f, 1.2f), new Color(0.58f, 0.22f, 0.92f), clues, false);
            AddAmbientMist(puddle.transform, new Color(0.65f, 0.28f, 0.98f, 0.45f));
            AddPointLight(env, new Vector3(2.2f, 1.0f, 5.1f), new Color(0.68f, 0.28f, 1f), 1.8f, 5f);
            CreateClueNode("C2_SolventResidue", "C2_01", ClueNode.Kind.Physical, ClueNode.Tool.GolemEye, ClueNode.State.Hidden,
                "Pungent, rapid-sublimating synthetic solvent pooling beside the vault door — harmless purple vapor, not dark magic.",
                new Vector3(2.2f, 0.35f, 5.1f), new Vector3(1.3f, 1.0f, 1.3f), clues, puddle, useGroundEmbers: true);

            // C3_01: Altered Cargo Ledger (-4.5, 1.8) in Room 1 — Alchemist's Work Desk + candlelight
            PlaceProp(Dfk + "Furnitures/dfk_desk_02.prefab", env, new Vector3(-4.5f, 0f, 1.8f), new Vector3(0f, 15f, 0f), Vector3.one);
            PlaceProp(Mfp + "Chairs/chair.001.prefab", env, new Vector3(-4.5f, 0f, 0.7f), new Vector3(0f, 195f, 0f), Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_CandleTriple_01.prefab", env, new Vector3(-5.0f, 0.85f, 2.0f), Vector3.zero, Vector3.one);
            AddPointLight(env, new Vector3(-4.5f, 1.8f, 1.8f), new Color(1f, 0.72f, 0.36f), 2.4f, 8f);
            var scroll = PlaceProp(Mfp + "Miscellaneous/scrolls.prefab", clues, new Vector3(-4.3f, 0.86f, 1.7f), new Vector3(0f, -20f, 0f), Vector3.one);
            CreateClueNode("C3_AlteredCargoLedger", "C3_01", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Shipment #408 ledger: 200 crates of Greater Restoration Draughts logged, yet zero Moon-Lily extract was ever purchased.",
                new Vector3(-4.4f, 1.0f, 1.7f), new Vector3(1.6f, 1.1f, 1.3f), clues, scroll, useGroundEmbers: true);

            // L1_01: Sub-floor potion rack alcove in Room 2 (0, 12) — completely concealed until AetherSight is unlocked (yellow embers)
            CreateClueNode("L1_RunicLog1_Alcove", "L1_01", ClueNode.Kind.Physical, ClueNode.Tool.AetherSight, ClueNode.State.Hidden,
                "Sub-floor alcove etching: pre-cataclysm equations for synthetic mana solvent volatility (Runic Log #1).",
                new Vector3(0f, 0.4f, 12f), new Vector3(1.8f, 1.2f, 1.8f), clues, visualChild: clues.gameObject, useGroundEmbers: true, isRunic: true);

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

            // Cool twilight directional moonlight angling through upper lancet windows
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    l.color = new Color(0.55f, 0.68f, 0.95f);
                    l.intensity = 1.0f;
                    l.transform.eulerAngles = new Vector3(42f, -35f, 0f);
                }
            }

            // Bounds: 20m wide (x = -10..10), 24m long (z = -6..18), 12m tall Bell Tower
            CreateWall("FloorCollider", new Vector3(0f, -0.25f, 6f), new Vector3(20f, 0.5f, 24f), null, env);
            CreateWall("CeilingCollider", new Vector3(0f, 12.25f, 6f), new Vector3(20f, 0.5f, 24f), null, env);
            BuildPerimeterColliders(env, new Vector3(0f, 0f, 6f), new Vector3(20f, 12.5f, 24f));

            // Ground floor tiles (x = -8..8, z = -4..16)
            for (int gx = -8; gx <= 8; gx += 4)
            for (int gz = -4; gz <= 16; gz += 4)
            {
                string tile = (gx + gz) % 8 == 0
                    ? Lpd + "02_Floors/SM_LPD_Floor_PavingFlagstoneWorn_01.prefab"
                    : Lpd + "02_Floors/SM_LPD_Floor_PavingFlagstone_01.prefab";
                PlaceProp(tile, env, new Vector3(gx, 0f, gz), Vector3.zero, Vector3.one);
            }

            // Roof: Nave ceiling at Y=8 (z = -4..8), Elevated Bell Tower ceiling at Y=12 (z = 12..16)
            for (int gx = -8; gx <= 8; gx += 4)
            {
                for (int gz = -4; gz <= 8; gz += 4)
                {
                    string ceil = Mathf.Abs(gx) <= 4
                        ? Lpd + "01_Structure/SM_LPD_Struct_CeilGroinRibbed_01.prefab"
                        : Lpd + "01_Structure/SM_LPD_Struct_CeilFlat_01.prefab";
                    PlaceProp(ceil, env, new Vector3(gx, 8.0f, gz), Vector3.zero, Vector3.one);
                }
                for (int gz = 12; gz <= 16; gz += 4)
                    PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_CeilTimberBoarded_01.prefab", env, new Vector3(gx, 12.0f, gz), Vector3.zero, Vector3.one);
            }

            // Perimeter walls: 2-tier in Nave (Y=0,4), 3-tier in Bell Tower (Y=0,4,8)
            for (int gx = -8; gx <= 8; gx += 4)
            {
                // North Bell Tower rear wall (z = 18)
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallAshlar_01.prefab", env, new Vector3(gx, 0f, 18f), new Vector3(0f, 180f, 0f), Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallWindowLancet_01.prefab", env, new Vector3(gx, 4f, 18f), new Vector3(0f, 180f, 0f), Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallWindowTwin_01.prefab", env, new Vector3(gx, 8f, 18f), new Vector3(0f, 180f, 0f), Vector3.one);

                // South Nave entrance wall (z = -6)
                string southLower = gx == 0 ? "01_Structure/SM_LPD_Struct_WallDoorway_01.prefab" : "01_Structure/SM_LPD_Struct_WallCoursed_01.prefab";
                PlaceProp(Lpd + southLower, env, new Vector3(gx, 0f, -6f), Vector3.zero, Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallWindowTwin_01.prefab", env, new Vector3(gx, 4f, -6f), Vector3.zero, Vector3.one);

                // Tower front clerestory wall above Nave roof (z = 10, Y = 8)
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallWindowLancet_01.prefab", env, new Vector3(gx, 8f, 10f), Vector3.zero, Vector3.one);
            }
            for (int gz = -4; gz <= 16; gz += 4)
            {
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallCoursed_01.prefab", env, new Vector3(-10f, 0f, gz), new Vector3(0f, 90f, 0f), Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallWindowLancet_01.prefab", env, new Vector3(-10f, 4f, gz), new Vector3(0f, 90f, 0f), Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallCoursed_01.prefab", env, new Vector3(10f, 0f, gz), new Vector3(0f, -90f, 0f), Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallWindowLancet_01.prefab", env, new Vector3(10f, 4f, gz), new Vector3(0f, -90f, 0f), Vector3.one);
                if (gz >= 12)
                {
                    PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallWindowTwin_01.prefab", env, new Vector3(-10f, 8f, gz), new Vector3(0f, 90f, 0f), Vector3.one);
                    PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallWindowTwin_01.prefab", env, new Vector3(10f, 8f, gz), new Vector3(0f, -90f, 0f), Vector3.one);
                }
            }
            foreach (var cPos in new[] { new Vector3(-10f, 0f, -6f), new Vector3(10f, 0f, -6f), new Vector3(-10f, 0f, 18f), new Vector3(10f, 0f, 18f) })
            {
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_CornerPostQuoined_01.prefab", env, cPos, Vector3.zero, Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_CornerPostQuoined_01.prefab", env, cPos + Vector3.up * 4f, Vector3.zero, Vector3.one);
                if (cPos.z > 0f)
                    PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_CornerPostQuoined_01.prefab", env, cPos + Vector3.up * 8f, Vector3.zero, Vector3.one);
            }

            // South Cathedral Exit Doorway (sealed double-leaf plank door backed by solid stone)
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_WallAshlar_01.prefab", env, new Vector3(0f, 0f, -6.5f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorFrameStone_01.prefab", env, new Vector3(0f, 0f, -6f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorPlank_01.prefab", env, new Vector3(0f, 0f, -6f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorPlank_01.prefab", env, new Vector3(-0.9f, 0f, -6f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_StairHalfFlight_01.prefab", env, new Vector3(0f, 0f, -5.2f), Vector3.zero, new Vector3(0.8f, 0.45f, 0.55f));
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_SconceIronLit_01.prefab", env, new Vector3(-2.2f, 2.2f, -5.7f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_SconceIronLit_01.prefab", env, new Vector3(2.2f, 2.2f, -5.7f), Vector3.zero, Vector3.one);

            // Cathedral Nave columns framing the central aisle and Bell Tower Arch at z = 10
            foreach (var colPos in new[] { new Vector3(-4.5f, 0f, -1f), new Vector3(4.5f, 0f, -1f), new Vector3(-4.5f, 0f, 5f), new Vector3(4.5f, 0f, 5f), new Vector3(-4.5f, 0f, 10f), new Vector3(4.5f, 0f, 10f) })
            {
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_PillarOrnate_01.prefab", env, colPos, Vector3.zero, Vector3.one);
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_PillarSquare_01.prefab", env, colPos + Vector3.up * 4f, Vector3.zero, Vector3.one);
            }
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_ArchFree_01.prefab", env, new Vector3(0f, 4.0f, 10f), Vector3.zero, new Vector3(1.4f, 1.2f, 1.0f));

            // Bell Tower Upper Gallery (Y = 4.0, surrounding open central Bell Well at x=-2.5..2.5, z=12..16)
            CreateWall("GalleryCollider_W", new Vector3(-6.5f, 3.8f, 14f), new Vector3(7f, 0.4f, 8f), null, env);
            CreateWall("GalleryCollider_E", new Vector3(6.5f, 3.8f, 14f), new Vector3(7f, 0.4f, 8f), null, env);
            CreateWall("GalleryCollider_N", new Vector3(0f, 3.8f, 17.0f), new Vector3(6f, 0.4f, 2.5f), null, env);
            CreateWall("GalleryCollider_FrontBridge", new Vector3(0f, 3.8f, 10.5f), new Vector3(6f, 0.4f, 2.8f), null, env);
            for (int gx = -8; gx <= 8; gx += 4)
            for (int gz = 12; gz <= 16; gz += 4)
            {
                if (gx == 0 && gz == 12) continue;
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_CeilTimberBoarded_01.prefab", env, new Vector3(gx, 4.0f, gz), Vector3.zero, Vector3.one);
            }
            // Flat front walkway deck at z = 10.0 connecting East stairs to West gallery (no overhead beam blocking bell view)
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_CeilTimberBoarded_01.prefab", env, new Vector3(0f, 4.0f, 10.0f), Vector3.zero, new Vector3(1.2f, 1.0f, 0.6f));

            // Gallery railings around the open Bell Well
            PlaceProp(Mfp + "Fences/Small/fence.001.prefab", env, new Vector3(-2.3f, 4.0f, 13.5f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Mfp + "Fences/Small/fence.001.prefab", env, new Vector3(2.3f, 4.0f, 13.5f), new Vector3(0f, 90f, 0f), Vector3.one);

            // Grounded Stone Staircase along the East Aisle (x = 7.6, rising z = 1 -> 10 from Y = 0 to Y = 4.0)
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_StairFlight_01.prefab", env, new Vector3(7.6f, 0f, 4.0f), Vector3.zero, new Vector3(0.95f, 1.0f, 1.4f));
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_PillarSquare_01.prefab", env, new Vector3(7.6f, 0f, 9.2f), Vector3.zero, new Vector3(1.2f, 1.0f, 1.2f));
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_CeilFlat_01.prefab", env, new Vector3(7.6f, 4.0f, 9.0f), Vector3.zero, new Vector3(0.7f, 1.0f, 0.6f));
            var stairRamp = new GameObject("EastStairRampCollider");
            stairRamp.transform.SetParent(env, false);
            stairRamp.transform.position = new Vector3(7.6f, 1.95f, 5.2f);
            stairRamp.transform.eulerAngles = new Vector3(-26f, 0f, 0f);
            stairRamp.AddComponent<BoxCollider>().size = new Vector3(2.8f, 0.3f, 9.2f);

            // Cathedral Nave & Bell Tower Decor
            var decor = new GameObject("BelfryDecor").transform;
            decor.SetParent(env, false);

            PlaceProp(Dfk + "Decoration/Textiles/dfk_carpet_black_small.prefab", decor, new Vector3(0f, 0.02f, 2.0f), Vector3.zero, new Vector3(1.2f, 1f, 3.2f));
            PlaceProp(Dfk + "Decoration/Flags/dfk_flag_02_a.prefab", decor, new Vector3(-9.7f, 2.4f, 0f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Dfk + "Decoration/Flags/dfk_flag_02_a.prefab", decor, new Vector3(-9.7f, 2.4f, 8f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Dfk + "Decoration/Flags/dfk_flag_02_a.prefab", decor, new Vector3(9.7f, 2.4f, 0f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Dfk + "Decoration/Flags/dfk_flag_02_a.prefab", decor, new Vector3(9.7f, 2.4f, 8f), new Vector3(0f, -90f, 0f), Vector3.one);

            // Nave pews & braziers (clear of East stair approach at x = 6..9)
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_BenchBacked_01.prefab", decor, new Vector3(-2.6f, 0f, 1.0f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_BenchBacked_01.prefab", decor, new Vector3(-2.6f, 0f, 3.2f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_BenchBacked_01.prefab", decor, new Vector3(2.6f, 0f, 1.0f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_BenchBacked_01.prefab", decor, new Vector3(2.6f, 0f, 3.2f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_BrazierTripodLit_01.prefab", decor, new Vector3(-2.6f, 0f, -1.8f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_BrazierTripodLit_01.prefab", decor, new Vector3(2.6f, 0f, -1.8f), Vector3.zero, Vector3.one);

            // West Nave: Accord's bell-ringer desk & bell rope coil beneath the bell tower
            PlaceProp(Dfk + "Furnitures/dfk_desk_02.prefab", decor, new Vector3(-7.6f, 0f, -1.5f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_ChairPlain_01.prefab", decor, new Vector3(-6.5f, 0f, -1.5f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_OilLampBrass_01.prefab", decor, new Vector3(-7.6f, 0.85f, -1.2f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_RopeDescentCoil_01.prefab", decor, new Vector3(-1.4f, 0f, 11.8f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_RopeDescentHanging_01.prefab", decor, new Vector3(-1.4f, 0.4f, 11.8f), Vector3.zero, new Vector3(1f, 1.5f, 1f));

            // Upper Bell Tower Gallery (Y = 4.0): maintenance shoring, cressets, trestle table
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_ShoringProps_01.prefab", decor, new Vector3(-5.2f, 4.0f, 13.4f), new Vector3(0f, 35f, 0f), Vector3.one);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_ShoringProps_01.prefab", decor, new Vector3(5.6f, 4.0f, 13.8f), new Vector3(0f, -35f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_CrateMedium_01.prefab", decor, new Vector3(-5.4f, 4.0f, 14.8f), new Vector3(0f, 12f, 0f), Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_CandleTriple_01.prefab", decor, new Vector3(-5.6f, 4.8f, 14.5f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "06_Furniture/SM_LPD_Furn_TableTrestle_01.prefab", decor, new Vector3(5.6f, 4.0f, 15.2f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_LanternHooded_01.prefab", decor, new Vector3(5.5f, 4.78f, 14.8f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_CressetLit_01.prefab", decor, new Vector3(-2.6f, 4.0f, 11.2f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_CressetLit_01.prefab", decor, new Vector3(2.6f, 4.0f, 11.2f), Vector3.zero, Vector3.one);

            AddPointLight(env, new Vector3(0f, 4.2f, 2.0f), new Color(0.75f, 0.84f, 1f), 3.0f, 16f);
            AddPointLight(env, new Vector3(0f, 9.2f, 14.0f), new Color(0.85f, 0.78f, 1f), 3.5f, 18f);

            var clues = new GameObject("Clues").transform;
            SceneManager.MoveGameObjectToScene(clues.gameObject, scene);

            // C1_02: Fractured Resonance Crystal — directly beneath the Great Bell on the ground floor (0, 0, 14)
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_PillarBroken_01.prefab", env, new Vector3(0f, 0f, 14.0f), Vector3.zero, new Vector3(0.7f, 0.45f, 0.7f));
            var crystal = PlaceProp(Dfk + "Lightsources/dfk_crystal_01_blue.prefab", clues, new Vector3(0f, 0.85f, 14.0f), new Vector3(12f, 20f, -10f), new Vector3(1.3f, 1.3f, 1.3f));
            AddPointLight(env, new Vector3(0f, 1.6f, 14.0f), new Color(0.35f, 0.78f, 1f), 2.2f, 7f);
            CreateClueNode("C1_FracturedCrystal", "C1_02", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Shattered acoustic focus crystal directly beneath the Great Bell. Radial shear fractures prove internal harmonic overload.",
                new Vector3(0f, 1.1f, 14.0f), new Vector3(1.5f, 1.6f, 1.5f), clues, crystal, useGroundEmbers: true);

            // C2_02: Dark Mana Dust on Clapper — Elevated Great Bell suspended over the open Bell Well (Y = 5.4..8.0, z = 14)
            var bellFrame = new GameObject("GreatBellHousing").transform;
            bellFrame.SetParent(env, false);
            PlaceProp(Mfp + "Columns/wood_column.001.prefab", bellFrame, new Vector3(-2.1f, 4.0f, 14.0f), Vector3.zero, new Vector3(1.1f, 1.45f, 1.1f));
            PlaceProp(Mfp + "Columns/wood_column.001.prefab", bellFrame, new Vector3(2.1f, 4.0f, 14.0f), Vector3.zero, new Vector3(1.1f, 1.45f, 1.1f));
            CreatePrimitiveMarker("BellCrossbeam", PrimitiveType.Cube, new Vector3(0f, 8.0f, 14.0f),
                new Vector3(4.8f, 0.35f, 0.4f), new Color(0.28f, 0.19f, 0.12f), bellFrame, false);

            var bellRoot = new GameObject("GreatBell").transform;
            bellRoot.SetParent(clues, false);
            bellRoot.position = new Vector3(0f, 6.2f, 14.0f);
            CreatePrimitiveMarker("BellCrown", PrimitiveType.Sphere, new Vector3(0f, 7.25f, 14.0f),
                new Vector3(1.6f, 1.0f, 1.6f), new Color(0.64f, 0.49f, 0.22f), bellRoot, false);
            CreatePrimitiveMarker("BellBody", PrimitiveType.Cylinder, new Vector3(0f, 6.55f, 14.0f),
                new Vector3(1.7f, 0.65f, 1.7f), new Color(0.60f, 0.46f, 0.20f), bellRoot, false);
            CreatePrimitiveMarker("BellRim", PrimitiveType.Cylinder, new Vector3(0f, 5.95f, 14.0f),
                new Vector3(2.15f, 0.2f, 2.15f), new Color(0.68f, 0.53f, 0.24f), bellRoot, false);
            var clapper = CreatePrimitiveMarker("BellClapper", PrimitiveType.Sphere, new Vector3(0f, 5.45f, 14.0f),
                new Vector3(0.48f, 0.48f, 0.48f), new Color(0.22f, 0.14f, 0.32f), bellRoot, false);
            AddAmbientMist(clapper.transform, new Color(0.48f, 0.20f, 0.75f, 0.45f));
            AddPointLight(env, new Vector3(0f, 5.5f, 14.0f), new Color(0.58f, 0.28f, 0.92f), 2.2f, 6f);
            CreateClueNode("C2_BellClapperDust", "C2_02", ClueNode.Kind.Physical, ClueNode.Tool.GolemEye, ClueNode.State.Hidden,
                "Black metallic soot on the bell clapper's strike face. Spectral scan reads an unnatural acoustic muting field.",
                new Vector3(0f, 5.45f, 14.0f), new Vector3(2.6f, 2.2f, 2.6f), clues, bellRoot.gameObject, useGroundEmbers: true);

            // C3_02: Blackmailer's Coercion Note (on crate behind timber shoring on upper West gallery)
            var note = PlaceProp(Mfp + "Miscellaneous/scroll.prefab", clues, new Vector3(-5.2f, 4.82f, 14.8f), new Vector3(0f, 25f, 0f), new Vector3(1.4f, 1.4f, 1.4f));
            CreateClueNode("C3_CoercionNote", "C3_02", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Crumpled letter addressed to Accord, sealed with the Veiled Circle's veil sigil. Threatens his sister Marian's asylum.",
                new Vector3(-5.2f, 4.95f, 14.8f), new Vector3(1.4f, 1.2f, 1.4f), clues, note, useGroundEmbers: true);

            // L2_02: Rear foundation beam behind the bell housing (Y = 4.0 gallery, z = 17.3) — concealed until AetherSight
            CreateClueNode("L2_RunicLog2_Beam", "L2_02", ClueNode.Kind.Physical, ClueNode.Tool.AetherSight, ClueNode.State.Hidden,
                "Ancient harmonic rune etched into the rear masonry beam: Acoustic Stabilization equations (Runic Log #2).",
                new Vector3(0f, 4.8f, 17.3f), new Vector3(1.8f, 1.4f, 1.4f), clues, visualChild: clues.gameObject, useGroundEmbers: true, isRunic: true);

            // Exit stairway down to Cathedral Courtyard / Nave
            CreateExitPortal("Exit_BelfryStair", 2, "Hub_CathedralCourtyard",
                "[L-Click] Descend to Cathedral",
                "Return down the belfry stairs to the Cathedral of Saint Althea.",
                new Vector3(0f, 1.2f, -5.0f), new Vector3(2.2f, 2.4f, 1.6f), clues);

            // Dynamic Active Goal controller for Case 2
            var ctrlGo = new GameObject("Case2ObjectiveController");
            SceneManager.MoveGameObjectToScene(ctrlGo, scene);
            var objCtrl = ctrlGo.AddComponent<InvestigationObjectiveController>();
            objCtrl.caseId = 2;
            objCtrl.requiredClueIds = new[] { "C1_02", "C2_02", "C3_02" };
            objCtrl.gatherObjectiveFormat = "Inspect the shattered crystal, climb the stone stairs to the Great Bell [Tab], and search the upper gallery ({0}/{1}).";
            objCtrl.boardObjective = "Evidence gathered. Open the Deduction Board [H] or descend to the Cathedral.";
            objCtrl.solvedObjective = "Board solved! Descend the belfry stairs to confront Accord.";

            SaveScene(scene, Case2ScenePath);
        }

        [MenuItem("Morsoul/3D/Build Case 3 - Whisperwood Grove")]
        public static void BuildCase3Whisperwood()
        {
            var scene = CreateBaseScene(
                Case3ScenePath,
                new Color(0.09f, 0.12f, 0.12f),
                new Color(0.13f, 0.16f, 0.17f),
                0.042f,
                new Vector3(0f, 1.08f, -5.5f),
                Vector3.zero);

            RenderSettings.skybox = null;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.06f, 0.08f, 0.09f, 1f);
            }

            var env = new GameObject("Environment").transform;
            SceneManager.MoveGameObjectToScene(env.gameObject, scene);

            // Dim, gloomy overcast moonlight filtering through dense canopy
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    l.color = new Color(0.45f, 0.56f, 0.64f);
                    l.intensity = 0.45f;
                    l.transform.eulerAngles = new Vector3(48f, -30f, 0f);
                }
            }

            // Custom sculpted Terrain (flat dirt ritual clearing + flat dirt exit trail + rolling forest hills)
            const string customDir = "Assets/Art/Environments/Custom/Whisperwood3D";
            if (!System.IO.Directory.Exists(customDir)) System.IO.Directory.CreateDirectory(customDir);

            var terrain = CreateWhisperwoodTerrain(customDir, env);

            // Invisible perimeter barrier enclosing the playable grove (x in [-14, 14], z in [-11.5, 20.5])
            BuildPerimeterColliders(env, new Vector3(0f, 0f, 4.5f), new Vector3(28f, 10f, 32f));

            var ritualMat = EnsureRitualCircleMaterial(customDir);

            // Forest & Vegetation using user-provided BrokenVector LowPolyTreePack trees
            var forest = new GameObject("ForestAndVegetation").transform;
            forest.SetParent(env, false);
            var rng = new System.Random(307);

            const string TreePack = "Assets/BrokenVector/LowPolyTreePack/Prefabs/";
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null)
            {
                foreach (var matName in new[] { "Normal.mat", "Cold.mat", "Dry.mat", "Fall.mat" })
                {
                    var tMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/BrokenVector/LowPolyTreePack/Materials/" + matName);
                    if (tMat != null && tMat.shader != urpLit)
                    {
                        var tex = tMat.GetTexture("_MainTex");
                        tMat.shader = urpLit;
                        if (tex != null && tMat.HasProperty("_BaseMap")) tMat.SetTexture("_BaseMap", tex);
                        if (tMat.HasProperty("_BaseColor")) tMat.SetColor("_BaseColor", Color.white);
                        if (tMat.HasProperty("_Smoothness")) tMat.SetFloat("_Smoothness", 0.1f);
                        EditorUtility.SetDirty(tMat);
                    }
                }
            }
            var treePrefabs = new[]
            {
                TreePack + "Tree Type0 01.prefab", TreePack + "Tree Type0 02.prefab", TreePack + "Tree Type0 03.prefab",
                TreePack + "Tree Type1 01.prefab", TreePack + "Tree Type1 02.prefab", TreePack + "Tree Type2 01.prefab",
                TreePack + "Tree Type3 01.prefab", TreePack + "Tree Type4 01.prefab", TreePack + "Tree Type5 01.prefab",
                TreePack + "Tree Type6 01.prefab", TreePack + "Tree Type7 01.prefab"
            };

            // Natural tree clusters enclosing the clearing, trail, and surrounding hills (out to +/-26m)
            Vector2[] clusterCenters =
            {
                new Vector2(-9.5f, -5.0f), new Vector2(9.5f, -4.5f),
                new Vector2(-11.5f, 2.5f), new Vector2(11.5f, 2.0f),
                new Vector2(-11.0f, 10.5f), new Vector2(11.0f, 10.0f),
                new Vector2(-9.0f, 17.5f), new Vector2(9.0f, 17.5f),
                new Vector2(-18.5f, -8.0f), new Vector2(18.5f, -7.5f),
                new Vector2(-19.5f, 4.5f), new Vector2(19.5f, 5.0f),
                new Vector2(-18.0f, 16.5f), new Vector2(18.0f, 16.5f),
                new Vector2(-6.5f, 23.0f), new Vector2(6.5f, 23.0f),
                new Vector2(0f, 25.0f), new Vector2(-14.5f, -15.5f), new Vector2(14.5f, -15.5f)
            };

            int treeIdx = 0;
            foreach (var center in clusterCenters)
            {
                int count = 4 + rng.Next(3); // 4-6 trees per natural cluster
                for (int k = 0; k < count; k++)
                {
                    float ang = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                    float rad = 1.2f + (float)rng.NextDouble() * 4.2f;
                    float tx = center.x + Mathf.Cos(ang) * rad;
                    float tz = center.y + Mathf.Sin(ang) * rad;

                    bool insideClearing = Vector2.Distance(new Vector2(tx, tz), new Vector2(0f, 7.5f)) < 8.0f;
                    bool onTrail = Mathf.Abs(tx) < 3.4f && tz >= -13.0f && tz <= 3.0f;
                    bool nearElder = Vector2.Distance(new Vector2(tx, tz), new Vector2(0f, 17.2f)) < 4.0f;
                    if (insideClearing || onTrail || nearElder) continue;

                    float ty = terrain.SampleHeight(new Vector3(tx, 0f, tz));
                    float sc = 0.85f + (float)rng.NextDouble() * 0.55f;
                    PlaceProp(treePrefabs[treeIdx % treePrefabs.Length], forest,
                        new Vector3(tx, ty - 0.15f, tz),
                        new Vector3(0f, (float)rng.NextDouble() * 360f, 0f),
                        Vector3.one * sc);
                    treeIdx++;
                }
            }

            // Interior bushes & undergrowth around clearing edge, trail borders, and tree bases
            for (int i = 0; i < 85; i++)
            {
                float x = Mathf.Lerp(-16.5f, 16.5f, (float)rng.NextDouble());
                float z = Mathf.Lerp(-13.5f, 22.0f, (float)rng.NextDouble());
                bool insideRitualDirt = Vector2.Distance(new Vector2(x, z), new Vector2(0f, 7.5f)) < 5.6f;
                bool onTrailCenter = Mathf.Abs(x) < 2.2f && z >= -12.5f && z <= 2.5f;
                bool nearAltar = Vector2.Distance(new Vector2(x, z), new Vector2(-4.2f, 7.5f)) < 2.0f;
                if (insideRitualDirt || onTrailCenter || nearAltar) continue;

                float y = terrain.SampleHeight(new Vector3(x, 0f, z));
                string bushPath = Bushes + $"P_Bush0{(i % 5) + 1}.prefab";
                float s = 0.8f + (float)rng.NextDouble() * 0.9f;
                PlaceProp(bushPath, forest, new Vector3(x, y - 0.08f, z), new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), Vector3.one * s);
            }

            // Dense vegetation + heavy white barrier fog ringing the invisible boundary (leaving South trail open)
            var barrierVisuals = new GameObject("BarrierFloraAndFog").transform;
            barrierVisuals.SetParent(env, false);
            for (int i = 0; i < 64; i++)
            {
                float side = i % 4;
                float bx, bz;
                if (side == 0) { bx = -14.2f + (float)(rng.NextDouble() * 1.4 - 0.7); bz = Mathf.Lerp(-11.5f, 20.5f, (i / 4) / 16f); }
                else if (side == 1) { bx = 14.2f + (float)(rng.NextDouble() * 1.4 - 0.7); bz = Mathf.Lerp(-11.5f, 20.5f, (i / 4) / 16f); }
                else if (side == 2) { bx = Mathf.Lerp(-14f, 14f, (i / 4) / 16f); bz = 20.5f + (float)(rng.NextDouble() * 1.4 - 0.7); }
                else
                {
                    bx = Mathf.Lerp(-14f, 14f, (i / 4) / 16f);
                    bz = -11.5f + (float)(rng.NextDouble() * 1.2 - 0.6);
                    if (Mathf.Abs(bx) < 2.6f) continue; // Keep South entrance path open
                }

                float by = terrain.SampleHeight(new Vector3(bx, 0f, bz));
                string bushPath = Bushes + $"P_Bush0{(i % 5) + 1}.prefab";
                float bs = 1.35f + (float)rng.NextDouble() * 0.75f;
                PlaceProp(bushPath, barrierVisuals, new Vector3(bx, by - 0.1f, bz), new Vector3(0f, (float)rng.NextDouble() * 360f, 0f), Vector3.one * bs);
            }

            // Two distinct white smoky fog systems: (A) Light ambient fog, (B) Heavy barrier fog
            var fogMat = EnsureFogParticleMaterial(customDir);
            AddAmbientGroundFog(env, new Vector3(0f, 0.9f, 4.5f), new Vector3(26f, 1.6f, 30f), fogMat);
            AddBoundaryFog(barrierVisuals, new Vector3(-14.2f, 1.5f, 4.5f), new Vector3(1.8f, 2.6f, 32f), fogMat);
            AddBoundaryFog(barrierVisuals, new Vector3(14.2f, 1.5f, 4.5f), new Vector3(1.8f, 2.6f, 32f), fogMat);
            AddBoundaryFog(barrierVisuals, new Vector3(0f, 1.5f, 20.6f), new Vector3(28f, 2.6f, 1.8f), fogMat);
            AddBoundaryFog(barrierVisuals, new Vector3(-8.4f, 1.5f, -11.6f), new Vector3(11.2f, 2.6f, 1.8f), fogMat);
            AddBoundaryFog(barrierVisuals, new Vector3(8.4f, 1.5f, -11.6f), new Vector3(11.2f, 2.6f, 1.8f), fogMat);

            // Natural stone rubble outcrops, fallen logs & stumps
            PlaceProp(Mfp + "Miscellaneous/half_tree.prefab", forest, new Vector3(-6.8f, terrain.SampleHeight(new Vector3(-6.8f, 0f, 1.5f)) - 0.06f, 1.5f), new Vector3(0f, 35f, 0f), Vector3.one * 0.45f);
            PlaceProp(Mfp + "Miscellaneous/half_tree.001.prefab", forest, new Vector3(6.4f, terrain.SampleHeight(new Vector3(6.4f, 0f, 3.2f)) - 0.06f, 3.2f), new Vector3(0f, -60f, 0f), Vector3.one * 0.42f);
            PlaceProp(Mfp + "Miscellaneous/trunk.prefab", forest, new Vector3(7.4f, terrain.SampleHeight(new Vector3(7.4f, 0f, 10.8f)) - 0.06f, 10.8f), new Vector3(0f, -28f, 0f), Vector3.one * 0.48f);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_RubbleHeapMound_01.prefab", forest, new Vector3(-8.5f, terrain.SampleHeight(new Vector3(-8.5f, 0f, 13.5f)) - 0.1f, 13.5f), new Vector3(0f, 40f, 0f), Vector3.one * 0.9f);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_RubbleHeapMound_01.prefab", forest, new Vector3(8.2f, terrain.SampleHeight(new Vector3(8.2f, 0f, 14.0f)) - 0.1f, 14.0f), new Vector3(0f, -55f, 0f), Vector3.one * 0.85f);

            // Elder Spirit Tree at North edge of the clearing (0, 0, 17.5)
            var elderRoot = new GameObject("ElderSpiritTree_Anchor").transform;
            elderRoot.SetParent(env, false);
            elderRoot.position = new Vector3(0f, 0f, 17.5f);
            PlaceProp(TreePack + "Tree Type0 01.prefab", elderRoot, new Vector3(0f, -0.2f, 17.5f), new Vector3(0f, 15f, 0f), Vector3.one * 1.65f);
            AddPointLight(env, new Vector3(0f, 3.6f, 14.2f), new Color(0.35f, 0.88f, 0.58f), 2.4f, 14f);

            // Flat Ritual Clearing: Glowing Spell Circle + Ceremonial Pillars + Stone Altar + Candle Clusters
            var ritual = new GameObject("RitualSite").transform;
            ritual.SetParent(env, false);

            var circleQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            circleQuad.name = "RitualSpellCircle";
            Object.DestroyImmediate(circleQuad.GetComponent<Collider>());
            circleQuad.transform.SetParent(ritual, false);
            circleQuad.transform.position = new Vector3(0f, 0.08f, 7.5f);
            circleQuad.transform.eulerAngles = new Vector3(90f, 0f, 0f);
            circleQuad.transform.localScale = new Vector3(7.2f, 7.2f, 1f);
            circleQuad.GetComponent<MeshRenderer>().sharedMaterial = ritualMat;

            // Ring of ritual candle clusters & weathered stone pillars framing the circle
            for (int c = 0; c < 6; c++)
            {
                float a = c * Mathf.PI * 2f / 6f;
                Vector3 cPos = new Vector3(Mathf.Cos(a) * 3.65f, 0.0f, 7.5f + Mathf.Sin(a) * 3.65f);
                PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_CandleTriple_01.prefab", ritual, cPos, Vector3.zero, Vector3.one * 1.15f);
                if (c % 2 == 0)
                {
                    Vector3 pPos = new Vector3(Mathf.Cos(a + 0.35f) * 5.2f, -0.05f, 7.5f + Mathf.Sin(a + 0.35f) * 5.2f);
                    PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_PillarBroken_01.prefab", ritual, pPos, new Vector3(0f, c * 60f, 0f), new Vector3(0.75f, 0.65f, 0.75f));
                }
            }
            AddPointLight(env, new Vector3(0f, 1.5f, 7.5f), new Color(0.75f, 0.24f, 0.92f), 2.2f, 11f);

            // Ceremonial Stone Altar on West edge of circle (-4.2, 0, 7.5) with candles, vessels & ritual materials
            EnsureSolidBox("RitualStoneAltar", new Vector3(-4.2f, 0.425f, 7.5f), new Vector3(1.1f, 0.85f, 1.8f), new Color(0.24f, 0.25f, 0.27f), ritual);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_CandleSingle_01.prefab", ritual, new Vector3(-4.45f, 0.85f, 8.15f), Vector3.zero, Vector3.one);
            PlaceProp(Dfk + "Lightsources/dfk_candle_02.prefab", ritual, new Vector3(-4.45f, 0.85f, 6.85f), Vector3.zero, Vector3.one);
            PlaceProp(Mfp + "Miscellaneous/On Table/bottle.002.prefab", ritual, new Vector3(-3.85f, 0.85f, 8.05f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_CressetLit_01.prefab", ritual, new Vector3(-4.8f, 0f, 6.1f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_CressetLit_01.prefab", ritual, new Vector3(-4.8f, 0f, 8.9f), Vector3.zero, Vector3.one);

            // East ritual preparation station (cauldron, ceremonial urns & mana canisters near siphon)
            PlaceProp(Mfp + "Miscellaneous/cauldron.prefab", ritual, new Vector3(1.6f, 0f, 13.6f), Vector3.zero, Vector3.one * 0.9f);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_UrnUrn_01.prefab", ritual, new Vector3(2.8f, 0f, 13.2f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_PotClay_01.prefab", ritual, new Vector3(-1.5f, 0f, 13.8f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_BarrelUpright_01.prefab", ritual, new Vector3(2.2f, 0f, 14.5f), Vector3.zero, Vector3.one * 0.85f);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_BrazierTripodLit_01.prefab", ritual, new Vector3(4.6f, 0f, 7.5f), Vector3.zero, Vector3.one);

            // Identifiable Open South Trail Exit: two lanterns flanking the open dirt path
            var exitGroup = new GameObject("GroveExitTrailMarkers").transform;
            exitGroup.SetParent(env, false);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_LanternFloor_01.prefab", exitGroup, new Vector3(-2.1f, 0f, -9.8f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_LanternFloor_01.prefab", exitGroup, new Vector3(2.1f, 0f, -9.8f), Vector3.zero, Vector3.one);
            AddPointLight(env, new Vector3(0f, 2.0f, -9.8f), new Color(1f, 0.78f, 0.45f), 2.2f, 9f);

            var clues = new GameObject("Clues").transform;
            SceneManager.MoveGameObjectToScene(clues.gameObject, scene);

            // C1_03: Brass Extraction Siphon at Elder Tree roots (grounded at y = 0, conduit plunging into root)
            var siphonPrefab = EnsureBrassSiphonPrefab(customDir);
            var siphon = (GameObject)PrefabUtility.InstantiatePrefab(siphonPrefab, clues);
            siphon.name = "BrassExtractionSiphon";
            siphon.transform.position = new Vector3(0f, 0f, 13.55f);
            AddAmbientMist(siphon.transform, new Color(0.25f, 0.85f, 0.55f, 0.45f));
            CreateClueNode("C1_ExtractionSiphon", "C1_03", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Industrial pneumatic tap driven deep into the Elder Spirit Tree's taproot, draining raw mana into pressurized canisters.",
                new Vector3(0f, 0.6f, 13.55f), new Vector3(1.5f, 1.5f, 1.5f), clues, siphon, useGroundEmbers: true);

            // C2_03: Inscribed Silver Ritual Dagger resting flush on top of the Stone Altar (y = 0.87)
            var daggerRoot = new GameObject("SilverRitualDagger").transform;
            daggerRoot.SetParent(clues, false);
            daggerRoot.position = new Vector3(-4.2f, 0.87f, 7.5f);
            CreatePrimitiveMarker("Blade", PrimitiveType.Cube, new Vector3(-4.2f, 0.87f, 7.65f),
                new Vector3(0.10f, 0.04f, 0.52f), new Color(0.84f, 0.88f, 0.94f), daggerRoot, false);
            CreatePrimitiveMarker("Crossguard", PrimitiveType.Cube, new Vector3(-4.2f, 0.87f, 7.38f),
                new Vector3(0.28f, 0.06f, 0.06f), new Color(0.62f, 0.54f, 0.28f), daggerRoot, false);
            CreatePrimitiveMarker("Hilt", PrimitiveType.Cube, new Vector3(-4.2f, 0.87f, 7.25f),
                new Vector3(0.06f, 0.06f, 0.20f), new Color(0.28f, 0.16f, 0.18f), daggerRoot, false);
            CreateClueNode("C2_RitualDagger", "C2_03", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Ceremonial silver dagger bearing the Veiled Circle's eclipsed eye insignia, coated with Case 1's solvent stabilizer.",
                new Vector3(-4.2f, 0.95f, 7.5f), new Vector3(1.4f, 1.1f, 1.4f), clues, daggerRoot.gameObject, useGroundEmbers: true);

            // C3_03: Vane's Discarded Ritual Cloak (grounded beside the altar)
            var cloak = PlaceProp(Dfk + "Decoration/Textiles/dfk_carpet_black_small.prefab", clues, new Vector3(-5.5f, 0.02f, 9.6f), new Vector3(0f, 35f, 0f), new Vector3(0.85f, 0.85f, 0.85f));
            AddAmbientMist(cloak.transform, new Color(0.65f, 0.28f, 0.95f, 0.35f));
            CreateClueNode("C3_VaneCloak", "C3_03", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Vane's discarded ritual cloak. Lingers with fanatical zeal and deceit tying him to the Sanctum Core ritual.",
                new Vector3(-5.5f, 0.45f, 9.6f), new Vector3(1.6f, 1.0f, 1.6f), clues, cloak, useGroundEmbers: true);

            // L3_03: Central Altar Ground (center of spell circle) — concealed until AetherSight is unlocked (yellow embers)
            CreateClueNode("L3_RunicLog3_AltarGround", "L3_03", ClueNode.Kind.Physical, ClueNode.Tool.AetherSight, ClueNode.State.Hidden,
                "Buried pre-cataclysm telemetry beneath the altar: Displacement Device Telemetry (Runic Log #3).",
                new Vector3(0f, 0.45f, 7.5f), new Vector3(1.8f, 1.2f, 1.8f), clues, visualChild: clues.gameObject, useGroundEmbers: true, isRunic: true);

            // Open Trail Exit back to Whisperwood Trail
            CreateExitPortal("Exit_WhisperwoodTrail", 3, "Hub_Wilderness",
                "[L-Click] Follow Trail to Whisperwood Outpost",
                "Walk back down the forest path to the Whisperwood Ranger Outpost.",
                new Vector3(0f, 1.2f, -10.5f), new Vector3(3.6f, 2.4f, 1.6f), clues);

            // Dynamic Active Goal controller for Case 3
            var ctrlGo = new GameObject("Case3ObjectiveController");
            SceneManager.MoveGameObjectToScene(ctrlGo, scene);
            var objCtrl = ctrlGo.AddComponent<InvestigationObjectiveController>();
            objCtrl.caseId = 3;
            objCtrl.requiredClueIds = new[] { "C1_03", "C2_03", "C3_03" };
            objCtrl.gatherObjectiveFormat = "Inspect the Elder Tree siphon, ritual dagger, and Vane's cloak ({0}/{1}).";
            objCtrl.boardObjective = "Evidence gathered. Open the Deduction Board [H] or return to Whisperwood Trail.";
            objCtrl.solvedObjective = "Board solved! Return to the Whisperwood Trail to confront Vane.";

            SaveScene(scene, Case3ScenePath);
        }

        [MenuItem("Morsoul/3D/Build Case 4 - Iron Hollow Sub-Facility")]
        public static void BuildCase4IronHollow()
        {
            var scene = CreateBaseScene(
                Case4ScenePath,
                new Color(0.14f, 0.12f, 0.11f),
                new Color(0.08f, 0.07f, 0.07f),
                0.025f,
                new Vector3(0f, 1.0f, 0.5f),
                Vector3.zero);

            // Underground sub-facility: disable outdoor Directional Light so no sky sunlight leaks through
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) l.gameObject.SetActive(false);
            }
            RenderSettings.skybox = null;

            const string customDir = "Assets/Art/Environments/Custom/IronHollow3D";
            if (!System.IO.Directory.Exists(customDir)) System.IO.Directory.CreateDirectory(customDir);
            var ih = EnsureIronHollowAssets(customDir);

            var env = new GameObject("Environment").transform;
            SceneManager.MoveGameObjectToScene(env.gameObject, scene);

            // Invisible perimeter colliders (x in [-10, 10], z in [-2, 22])
            BuildPerimeterColliders(env, new Vector3(0f, 0f, 10f), new Vector3(20f, 4.5f, 24f));

            // 4m x 4m stone & industrial floor tiles (-8..8 in X, 0..20 in Z)
            for (int gx = -8; gx <= 8; gx += 4)
            for (int gz = 0; gz <= 20; gz += 4)
            {
                string tile = "02_Floors/SM_LPD_Floor_PavingFlagstone_01.prefab";
                if (gz < 12 && gx == 0 && (gz == 4 || gz == 8))
                    tile = "02_Floors/SM_LPD_Floor_PavingChannelStraight_01.prefab";
                else if (gz < 12 && (gx + gz) % 8 == 0)
                    tile = "02_Floors/SM_LPD_Floor_PavingFlagstoneWorn_01.prefab";
                PlaceProp(Lpd + tile, env, new Vector3(gx, 0f, gz), Vector3.zero, Vector3.one);
            }

            // Sealed stone & iron-beamed ceiling at Y = 4.0 (no wooden ceiling gaps)
            for (int gx = -8; gx <= 8; gx += 4)
            for (int gz = 0; gz <= 20; gz += 4)
            {
                string ceil = (gz < 12 && Mathf.Abs(gx) == 4)
                    ? "01_Structure/SM_LPD_Struct_CeilBeams_01.prefab"
                    : "01_Structure/SM_LPD_Struct_CeilFlat_01.prefab";
                PlaceProp(Lpd + ceil, env, new Vector3(gx, 4.0f, gz), Vector3.zero, Vector3.one);
            }

            // Stone perimeter walls (x = +/-10, z = -2 / +22)
            for (int gx = -8; gx <= 8; gx += 4)
            {
                string northWall = gx == 0 ? "01_Structure/SM_LPD_Struct_WallNiche_01.prefab" : "01_Structure/SM_LPD_Struct_WallCracked_01.prefab";
                PlaceProp(Lpd + northWall, env, new Vector3(gx, 0f, 22f), new Vector3(0f, 180f, 0f), Vector3.one);
                string southWall = gx == 0 ? "01_Structure/SM_LPD_Struct_WallDoorway_01.prefab" : "01_Structure/SM_LPD_Struct_WallAshlar_01.prefab";
                PlaceProp(Lpd + southWall, env, new Vector3(gx, 0f, -2f), Vector3.zero, Vector3.one);
            }
            for (int gz = 0; gz <= 20; gz += 4)
            {
                string wallType = gz < 12 ? "01_Structure/SM_LPD_Struct_WallAshlar_01.prefab" : "01_Structure/SM_LPD_Struct_WallCracked_01.prefab";
                PlaceProp(Lpd + wallType, env, new Vector3(-10f, 0f, gz), new Vector3(0f, 90f, 0f), Vector3.one);
                PlaceProp(Lpd + wallType, env, new Vector3(10f, 0f, gz), new Vector3(0f, -90f, 0f), Vector3.one);
            }

            // Structural stone pillars (replacing excessive wooden shoring)
            foreach (var p in new[] { new Vector3(-4f, 0f, 3.5f), new Vector3(4f, 0f, 3.5f), new Vector3(-4f, 0f, 16f), new Vector3(4f, 0f, 16f) })
                PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_PillarSquare_01.prefab", env, p, Vector3.zero, Vector3.one);

            // --- PRODUCTION WORKFLOW IN PROCESSING BAY (z = 0..12) ---
            var facility = new GameObject("IndustrialFacility").transform;
            facility.SetParent(env, false);

            // 1. Raw Material Receiving & Coal/Ore Storage (SE: x = 4.5..9.0, z = 0.5..4.0)
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_HandcartLoaded_01.prefab", facility, new Vector3(6.6f, 0f, 1.2f), new Vector3(0f, -35f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_BucketIron_01.prefab", facility, new Vector3(5.2f, 0f, 1.4f), Vector3.zero, Vector3.one);
            CreateCoalOrOrePile("CoalStoragePile", new Vector3(8.2f, 0f, 2.2f), ih.coalMat, facility);
            CreateCoalOrOrePile("RawIronOrePile", new Vector3(8.2f, 0f, 4.2f), ih.ironWeatheredMat, facility);

            // 2. Ore Crushing & Sorting Station (East: x = 7.4, z = 6.8)
            var crusher = (GameObject)PrefabUtility.InstantiatePrefab(ih.oreCrusherPrefab, facility);
            crusher.transform.position = new Vector3(7.4f, 0f, 6.8f);
            crusher.transform.eulerAngles = new Vector3(0f, -90f, 0f);

            // 3. Smelting & Refinement (West: Blast Furnace at (-8.0, 0, 5.5) + Refining Vat at (-5.2, 0, 9.2))
            var furnace = (GameObject)PrefabUtility.InstantiatePrefab(ih.blastFurnacePrefab, facility);
            furnace.transform.position = new Vector3(-8.0f, 0f, 5.5f);
            furnace.transform.eulerAngles = new Vector3(0f, 90f, 0f);
            AddPointLight(env, new Vector3(-6.8f, 1.1f, 5.5f), new Color(1f, 0.45f, 0.14f), 2.8f, 9f);

            var vat = (GameObject)PrefabUtility.InstantiatePrefab(ih.refiningVatPrefab, facility);
            vat.transform.position = new Vector3(-5.2f, 0f, 9.2f);

            // 4. Connected Metal Pipelines (Blast Furnace -> C1 Valve Manifold -> C2 Conduit Junction -> Refining Vat & Sub-Lab)
            BuildPipeRun(facility, "Pipe_FurnaceToValve", new Vector3(-7.0f, 1.2f, 5.8f), new Vector3(-4.5f, 1.2f, 6.0f), 0.11f, ih.ironDarkMat, ih.copperMat);
            BuildPipeRun(facility, "Pipe_ValveToVat", new Vector3(-4.5f, 1.2f, 6.0f), new Vector3(-5.2f, 1.2f, 8.5f), 0.10f, ih.ironDarkMat, ih.copperMat);
            BuildPipeRun(facility, "Pipe_ValveToJunction", new Vector3(-4.5f, 0.45f, 6.0f), new Vector3(-0.7f, 0.45f, 8.0f), 0.09f, ih.copperMat, ih.ironDarkMat);
            BuildPipeRun(facility, "Pipe_JunctionToSubLab", new Vector3(0.0f, 0.45f, 8.0f), new Vector3(0.0f, 0.45f, 11.8f), 0.09f, ih.copperMat, ih.ironDarkMat);
            BuildPipeRun(facility, "Pipe_OverheadMain", new Vector3(-8.0f, 3.3f, 5.5f), new Vector3(7.4f, 3.3f, 6.8f), 0.12f, ih.ironDarkMat, ih.ironWeatheredMat);
            BuildPipeRun(facility, "Pipe_OverheadDropToCrusher", new Vector3(7.4f, 3.3f, 6.8f), new Vector3(7.4f, 1.95f, 6.8f), 0.11f, ih.ironDarkMat, ih.copperMat);
            BuildPipeRun(facility, "Pipe_OverheadSupportL", new Vector3(-3.0f, 4.0f, 5.9f), new Vector3(-3.0f, 3.3f, 5.9f), 0.06f, ih.ironWeatheredMat, ih.ironDarkMat);
            BuildPipeRun(facility, "Pipe_OverheadSupportR", new Vector3(3.0f, 4.0f, 6.43f), new Vector3(3.0f, 3.3f, 6.43f), 0.06f, ih.ironWeatheredMat, ih.ironDarkMat);

            // 5. Cooling, Anvil & Refined Iron Ingot Storage (NE: x = 4.5..8.5, z = 9.2..11.0)
            PlaceProp(Mfp + "Miscellaneous/anvil.prefab", facility, new Vector3(5.2f, 0f, 9.6f), new Vector3(0f, -35f, 0f), Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_BucketIron_01.prefab", facility, new Vector3(4.3f, 0f, 10.2f), Vector3.zero, Vector3.one);
            CreateIngotStack("RefinedIngotPallet", new Vector3(7.5f, 0f, 10.2f), ih.ingotMat, ih.ironDarkMat, facility);

            // Soot & rubble accents + wall sconces (iron industrial lighting, minimal wood)
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_RubbleHeapAgainst_01.prefab", env, new Vector3(-9.2f, 0f, 1.8f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_RubbleHeapAgainst_01.prefab", env, new Vector3(9.2f, 0f, 9.2f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_DebrisScatter_01.prefab", env, new Vector3(-3.2f, 0.02f, 2.5f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_BrazierTripodLit_01.prefab", env, new Vector3(-2.6f, 0f, 1.8f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_BrazierTripodLit_01.prefab", env, new Vector3(2.6f, 0f, 1.8f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_SconceIronLit_01.prefab", env, new Vector3(-9.7f, 2.3f, 3.5f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_SconceIronLit_01.prefab", env, new Vector3(9.7f, 2.3f, 3.5f), new Vector3(0f, -90f, 0f), Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_SconceIronLit_01.prefab", env, new Vector3(-9.7f, 2.3f, 9.5f), new Vector3(0f, 90f, 0f), Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_SconceIronLit_01.prefab", env, new Vector3(9.7f, 2.3f, 9.5f), new Vector3(0f, -90f, 0f), Vector3.one);

            // Pre-Cataclysm Sub-Lab decor (z = 12..22): stone/iron lab feel
            PlaceProp(Dfk + "Lightsources/dfk_crystal_01_blue.prefab", env, new Vector3(-2.6f, 0f, 20.5f), Vector3.zero, new Vector3(1.1f, 1.4f, 1.1f));
            PlaceProp(Dfk + "Lightsources/dfk_crystal_01_blue.prefab", env, new Vector3(2.6f, 0f, 20.5f), Vector3.zero, new Vector3(1.1f, 1.4f, 1.1f));
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_UrnUrn_01.prefab", env, new Vector3(-7.5f, 0f, 20.5f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "05_Containers/SM_LPD_Crate_UrnUrn_01.prefab", env, new Vector3(7.5f, 0f, 20.5f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_ChandelierRing_01.prefab", env, new Vector3(0f, 3.8f, 16.5f), Vector3.zero, Vector3.one);

            // Steam vents along outer processing bay edges
            var steamL = new GameObject("SteamVent_Left");
            steamL.transform.SetParent(env, false);
            steamL.transform.position = new Vector3(-6.2f, 0.2f, 3.8f);
            AddAmbientMist(steamL.transform, new Color(0.85f, 0.88f, 0.92f, 0.35f));
            var steamR = new GameObject("SteamVent_Right");
            steamR.transform.SetParent(env, false);
            steamR.transform.position = new Vector3(6.2f, 0.2f, 3.8f);
            AddAmbientMist(steamR.transform, new Color(0.85f, 0.88f, 0.92f, 0.35f));

            // Midway stone partition wall at z=12 separating processing bay from Pre-Cataclysm Sub-Lab
            for (int gx = -8; gx <= 8; gx += 4)
            {
                string partWall = gx == 0 ? "01_Structure/SM_LPD_Struct_WallDoorway_01.prefab" : "01_Structure/SM_LPD_Struct_WallAshlar_01.prefab";
                PlaceProp(Lpd + partWall, env, new Vector3(gx, 0f, 12f), Vector3.zero, Vector3.one);
            }
            CreateWall("SubLabCollider_West", new Vector3(-5.75f, 2.25f, 12f), new Vector3(8.5f, 4.5f, 0.6f), null, env);
            CreateWall("SubLabCollider_East", new Vector3(5.75f, 2.25f, 12f), new Vector3(8.5f, 4.5f, 0.6f), null, env);

            // Deep-shaft elevator entrance/exit on South wall (z = -2.0) — backed by solid black shaft wall
            EnsureSolidBox("ElevatorShaftBlackWall", new Vector3(0f, 1.6f, -2.25f), new Vector3(3.0f, 3.4f, 0.25f), new Color(0.02f, 0.02f, 0.025f), env);
            var elevator = new GameObject("DeepShaftElevator").transform;
            elevator.SetParent(env, false);
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorFrameStone_01.prefab", elevator, new Vector3(0f, 0f, -1.95f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_FloorGrate_01.prefab", elevator, new Vector3(0f, 0.03f, -1.2f), Vector3.zero, new Vector3(1.2f, 1f, 1.2f));
            PlaceProp(Lpd + "01_Structure/SM_LPD_Struct_RopeDescentHanging_01.prefab", elevator, new Vector3(-0.7f, 0f, -1.6f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_LanternFloor_01.prefab", elevator, new Vector3(-1.6f, 0f, -1.3f), Vector3.zero, Vector3.one);
            PlaceProp(Lpd + "04_Lighting/SM_LPD_Light_LanternFloor_01.prefab", elevator, new Vector3(1.6f, 0f, -1.3f), Vector3.zero, Vector3.one);

            // Stasis Chamber anchor inside Pre-Cataclysm Sub-Lab (z = 16.5)
            EnsureSolidBox("StasisChamberDais", new Vector3(-4.2f, 0.2f, 16.5f), new Vector3(3.2f, 0.4f, 3.2f), new Color(0.18f, 0.22f, 0.26f), env);
            PlaceProp(Dfk + "Lightsources/dfk_crystal_01_blue.prefab", env, new Vector3(-4.2f, 0.45f, 16.5f), Vector3.zero, new Vector3(1.8f, 2.2f, 1.8f));

            // Warm forge lighting in processing bay + cool cyan pre-cataclysm lighting in sub-lab
            AddPointLight(env, new Vector3(0f, 3.5f, 5f), new Color(1f, 0.65f, 0.38f), 2.8f, 18f);
            AddPointLight(env, new Vector3(0f, 2.8f, -1.2f), new Color(1f, 0.78f, 0.48f), 2.2f, 10f);
            AddPointLight(env, new Vector3(0f, 3.5f, 17f), new Color(0.45f, 0.85f, 1f), 2.6f, 16f);
            AddPointLight(env, new Vector3(-4.2f, 2.5f, 16.5f), new Color(0.35f, 0.92f, 1f), 2.0f, 9f);

            var clues = new GameObject("Clues").transform;
            SceneManager.MoveGameObjectToScene(clues.gameObject, scene);

            // C1_04: Tampered Pneumatic Valve Manifold (connected to Furnace & Vat pipelines)
            var valve = (GameObject)PrefabUtility.InstantiatePrefab(ih.valveManifoldPrefab, clues);
            valve.name = "PneumaticValves";
            valve.transform.position = new Vector3(-4.5f, 0f, 6.0f);
            CreateClueNode("C1_PneumaticValves", "C1_04", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "High-pressure pneumatic release valves jammed with iron wedge pins to force catastrophic backflow.",
                new Vector3(-4.5f, 1.0f, 6.0f), new Vector3(1.5f, 1.6f, 1.5f), clues, valve, useGroundEmbers: true);

            // C2_04: Chemically Dissolved Bypass Conduits (center junction) — requires GolemEye
            var conduit = (GameObject)PrefabUtility.InstantiatePrefab(ih.conduitJunctionPrefab, clues);
            conduit.name = "DissolvedConduits";
            conduit.transform.position = new Vector3(0f, 0f, 8.0f);
            AddAmbientMist(conduit.transform, new Color(0.65f, 0.35f, 0.95f, 0.4f));
            CreateClueNode("C2_DissolvedConduits", "C2_04", ClueNode.Kind.Physical, ClueNode.Tool.GolemEye, ClueNode.State.Hidden,
                "Copper control wiring corroded into slag. Golem Eye confirms the same synthetic mana solvent from Case 1 and Case 3.",
                new Vector3(0f, 0.55f, 8.0f), new Vector3(1.8f, 1.2f, 1.4f), clues, conduit, useGroundEmbers: true);

            // C3_04: Pre-Cataclysm Sub-Lab Blast Door (closed across z=12 doorway, bound by runic seal)
            PlaceProp(Lpd + "03_Doors/SM_LPD_Door_DoorFrameStone_01.prefab", env, new Vector3(0f, 0f, 12f), Vector3.zero, Vector3.one);
            var blastDoor = (GameObject)PrefabUtility.InstantiatePrefab(ih.blastDoorPrefab, clues);
            blastDoor.name = "SubLabBlastDoorMesh";
            blastDoor.transform.position = new Vector3(-0.75f, 0f, 12f);
            blastDoor.transform.eulerAngles = Vector3.zero;
            CreateClueNode("C3_SubLabBlastDoor", "C3_04", ClueNode.Kind.Physical, ClueNode.Tool.None, ClueNode.State.Revealed,
                "Massive pre-cataclysm blast door sealed shut across the sub-lab threshold.",
                new Vector3(-0.75f, 1.2f, 11.65f), new Vector3(1.2f, 2.2f, 0.25f), clues, blastDoor, useGroundEmbers: true);

            // Runic seal blocking the z=12 doorway until revealed with [1] Aether Sight
            var sealGo = new GameObject("SubLabBlastDoorSeal");
            sealGo.layer = InteractableLayer;
            sealGo.transform.SetParent(clues, false);
            sealGo.transform.position = new Vector3(0f, 1.5f, 12f);
            var sealCol = sealGo.AddComponent<BoxCollider>();
            sealCol.size = new Vector3(3.0f, 3.0f, 0.4f);
            sealCol.isTrigger = false;
            var sealLock = sealGo.AddComponent<LockableInteractable>();
            sealLock.requiredItemId = "";
            sealLock.requireBoardSolved = false;
            sealLock.requireAetherSight = true;
            sealLock.boardCaseId = 4;
            sealLock.lockedPromptAction = "[Locked] Runic Blast Door Seal";
            sealLock.unlockedPromptAction = "[L-Click] Dispel Runic Seal & Open Door";
            sealLock.lockedDescription = "An ancient runic seal binds the blast door. Activate [1] Aether Sight to reveal and dispel it.";
            sealLock.unlockedDescription = "Runic seal revealed by Aether Sight. Dispel the barrier to enter the Pre-Cataclysm Sub-Lab.";
            sealLock.onInteractSuccess ??= new UnityEngine.Events.UnityEvent();
            UnityEventTools.AddBoolPersistentListener(sealLock.onInteractSuccess, sealGo.SetActive, false);
            UnityEventTools.AddBoolPersistentListener(sealLock.onInteractSuccess, blastDoor.SetActive, false);

            // L4_04: Ancient Primary Terminal inside sub-lab (z=18.5) — concealed until AetherSight+GolemEye (yellow embers)
            var terminal = PlaceProp(Dfk + "Furnitures/dfk_desk_02.prefab", clues, new Vector3(0f, 0f, 18.5f), Vector3.zero, Vector3.one);
            CreateClueNode("L4_RunicLog4_Terminal", "L4_04", ClueNode.Kind.Physical, ClueNode.Tool.Both, ClueNode.State.Hidden,
                "Decrypted pre-cataclysm primary terminal: Planetary Stabilizer Console Blueprints (Runic Log #4).",
                new Vector3(0f, 1.1f, 18.5f), new Vector3(1.8f, 1.6f, 1.4f), clues, terminal, useGroundEmbers: true, isRunic: true);

            // Exit Shaft Elevator back to Iron Hollow surface
            CreateExitPortal("Exit_ShaftElevator", 4, "Hub_IronHollow",
                "[L-Click] Ascend Shaft Elevator",
                "Ride the deep-shaft elevator back to the Iron Hollow surface.",
                new Vector3(0f, 1.2f, -2.0f), new Vector3(2.6f, 2.4f, 1.6f), clues);

            // Dynamic Active Goal controller for Case 4
            var ctrlGo = new GameObject("Case4ObjectiveController");
            SceneManager.MoveGameObjectToScene(ctrlGo, scene);
            var objCtrl = ctrlGo.AddComponent<InvestigationObjectiveController>();
            objCtrl.caseId = 4;
            objCtrl.requiredClueIds = new[] { "C1_04", "C2_04", "C3_04" };
            objCtrl.gatherObjectiveFormat = "Inspect pneumatic valves, scan bypass conduits [Tab], and check the blast door ({0}/{1}).";
            objCtrl.boardObjective = "Evidence gathered. Open the Deduction Board [H] or ride the Shaft Elevator up.";
            objCtrl.solvedObjective = "Board solved! Ride the Shaft Elevator to the surface to confront Harlan.";

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
            {
                // SharedRoot already contains Player/Main Camera; remove DefaultGameObjects' static Main Camera
                var defaultCam = GameObject.Find("Main Camera");
                if (defaultCam != null) Object.DestroyImmediate(defaultCam);
                PrefabUtility.InstantiatePrefab(sharedPrefab, scene);
            }
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
            GameObject visualChild = null, bool useGroundEmbers = false, bool isRunic = false)
        {
            var go = new GameObject(name);
            go.layer = InteractableLayer;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var bc = go.AddComponent<BoxCollider>();
            bc.size = colliderSize;
            bc.isTrigger = true;

            var clue = go.AddComponent<ClueNode>();
            clue.id = clueId;
            clue.kind = kind;
            clue.requiredTool = tool;
            clue.state = state;
            clue.defaultDescription = description;

            if (useGroundEmbers)
            {
                var embersPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Ground Embers Variant.prefab");
                ParticleSystem ps = null;
                if (embersPrefab != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(embersPrefab, go.transform);
                    inst.name = "Ground Embers";
                    inst.transform.localPosition = Vector3.zero;
                    inst.transform.localRotation = Quaternion.identity;
                    inst.transform.localScale = new Vector3(0.01f, 0.2f, 0.01f);
                    ps = inst.GetComponent<ParticleSystem>();
                }
                if (ps == null)
                {
                    var psGo = new GameObject("Ground Embers");
                    psGo.transform.SetParent(go.transform, false);
                    ps = psGo.AddComponent<ParticleSystem>();
                }
                var main = ps.main;
                main.playOnAwake = false;
                main.startLifetime = 1f;
                if (isRunic)
                    main.startColor = new Color(1f, 0.85f, 0.15f, 1f);
                clue.scanParticles = ps;
            }
            else
            {
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
            }

            if (visualChild == null)
                CreatePrimitiveMarker("Marker", PrimitiveType.Sphere, pos, Vector3.one * 0.25f, new Color(0.4f, 0.85f, 1f), go.transform, false);
            else if (visualChild != parent.gameObject)
                clue.inspectVisual = visualChild;
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
            var psr = go.GetComponent<ParticleSystemRenderer>();
            const string sharedFogDir = "Assets/Art/Environments/Custom/Whisperwood3D";
            if (!System.IO.Directory.Exists(sharedFogDir)) System.IO.Directory.CreateDirectory(sharedFogDir);
            var mistMat = AssetDatabase.LoadAssetAtPath<Material>(sharedFogDir + "/BoundaryFog.mat") ?? EnsureFogParticleMaterial(sharedFogDir);
            if (psr != null && mistMat != null) psr.sharedMaterial = mistMat;
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

        // --- Whisperwood 3D Helpers ------------------------------------------

        static Terrain CreateWhisperwoodTerrain(string customDir, Transform parent)
        {
            const float sizeXZ = 60f;
            const float maxH = 10f;
            const float originX = -30f;
            const float originZ = -24f;
            const int hRes = 129;
            const int aRes = 128;

            var mossTex = EnsureProceduralNoiseTexture(customDir + "/MossGrass.png", new Color(0.11f, 0.24f, 0.12f), new Color(0.17f, 0.32f, 0.15f), 11);
            var dirtTex = EnsureProceduralNoiseTexture(customDir + "/ForestDirt.png", new Color(0.38f, 0.25f, 0.15f), new Color(0.50f, 0.34f, 0.21f), 29);

            var mossLayer = EnsureTerrainLayer(customDir + "/MossGrassLayer.terrainlayer", mossTex, 6f);
            var dirtLayer = EnsureTerrainLayer(customDir + "/ForestDirtLayer.terrainlayer", dirtTex, 5f);

            string tdPath = customDir + "/WhisperwoodTerrain.asset";
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(tdPath) != null)
                AssetDatabase.DeleteAsset(tdPath);

            var td = new TerrainData
            {
                heightmapResolution = hRes,
                alphamapResolution = aRes,
                size = new Vector3(sizeXZ, maxH, sizeXZ)
            };
            AssetDatabase.CreateAsset(td, tdPath);
            td.terrainLayers = new[] { mossLayer, dirtLayer };

            var heights = new float[hRes, hRes];
            for (int iz = 0; iz < hRes; iz++)
            {
                float wz = originZ + (iz / (float)(hRes - 1)) * sizeXZ;
                for (int ix = 0; ix < hRes; ix++)
                {
                    float wx = originX + (ix / (float)(hRes - 1)) * sizeXZ;

                    float dClearing = Vector2.Distance(new Vector2(wx, wz), new Vector2(0f, 7.5f));
                    float dElder = Vector2.Distance(new Vector2(wx, wz), new Vector2(0f, 16.2f));
                    float trailBlendZ = (wz >= -11.5f && wz <= 2.5f) ? 0f : Mathf.Min(Mathf.Abs(wz + 11.5f), Mathf.Abs(wz - 2.5f));
                    float dTrail = Mathf.Sqrt(Mathf.Pow(Mathf.Max(0f, Mathf.Abs(wx) - 2.2f), 2f) + trailBlendZ * trailBlendZ);

                    float flatMask = Mathf.Min(
                        Mathf.InverseLerp(6.2f, 11.5f, dClearing),
                        Mathf.Min(
                            Mathf.InverseLerp(3.2f, 6.8f, dElder),
                            Mathf.InverseLerp(0.0f, 3.8f, dTrail)));
                    flatMask = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(flatMask));

                    float n1 = Mathf.PerlinNoise((wx + 100f) * 0.075f, (wz + 100f) * 0.075f);
                    float n2 = Mathf.PerlinNoise((wx + 240f) * 0.16f, (wz + 240f) * 0.16f) * 0.35f;
                    float hill = (0.8f + (n1 + n2) * 2.6f) * flatMask;
                    heights[iz, ix] = Mathf.Clamp01(hill / maxH);
                }
            }
            td.SetHeights(0, 0, heights);

            var alphas = new float[aRes, aRes, 2];
            for (int iz = 0; iz < aRes; iz++)
            {
                float wz = originZ + (iz / (float)(aRes - 1)) * sizeXZ;
                for (int ix = 0; ix < aRes; ix++)
                {
                    float wx = originX + (ix / (float)(aRes - 1)) * sizeXZ;
                    float dClearing = Vector2.Distance(new Vector2(wx, wz), new Vector2(0f, 7.5f));
                    float clearingDirt = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5.2f, 6.8f, dClearing));

                    float trailDirt = 0f;
                    if (wz >= -11.5f && wz <= 2.5f)
                        trailDirt = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.5f, 2.5f, Mathf.Abs(wx)));

                    float dirt = Mathf.Clamp01(Mathf.Max(clearingDirt, trailDirt));
                    alphas[iz, ix, 0] = 1f - dirt;
                    alphas[iz, ix, 1] = dirt;
                }
            }
            td.SetAlphamaps(0, 0, alphas);
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();

            var terrainGo = Terrain.CreateTerrainGameObject(td);
            terrainGo.name = "WhisperwoodTerrain";
            terrainGo.transform.SetParent(parent, false);
            terrainGo.transform.position = new Vector3(originX, 0f, originZ);
            return terrainGo.GetComponent<Terrain>();
        }

        static TerrainLayer EnsureTerrainLayer(string assetPath, Texture2D diffuse, float tileSize)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(assetPath);
            if (existing != null) AssetDatabase.DeleteAsset(assetPath);
            var layer = new TerrainLayer
            {
                diffuseTexture = diffuse,
                tileSize = new Vector2(tileSize, tileSize),
                smoothness = 0.08f
            };
            AssetDatabase.CreateAsset(layer, assetPath);
            return layer;
        }

        static Texture2D EnsureProceduralNoiseTexture(string pngPath, Color cA, Color cB, int seed)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = Mathf.PerlinNoise((x + seed * 17) * 0.14f, (y + seed * 31) * 0.14f);
                    float fine = Mathf.PerlinNoise((x + seed * 7) * 0.45f, (y + seed * 13) * 0.45f) * 0.25f;
                    pixels[y * size + x] = Color.Lerp(cA, cB, Mathf.Clamp01(n * 0.8f + fine));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        }

        static Material EnsureFogParticleMaterial(string customDir)
        {
            const int size = 128;
            string pngPath = customDir + "/BoundaryFogPuff.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            var puffTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);

            string matPath = customDir + "/BoundaryFog.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                AssetDatabase.DeleteAsset(matPath);

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.mainTexture = puffTex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", puffTex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        static void AddAmbientGroundFog(Transform parent, Vector3 pos, Vector3 boxSize, Material fogMat)
        {
            var go = new GameObject("AmbientGroundFog");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (psr != null && fogMat != null) psr.sharedMaterial = fogMat;
            var main = ps.main;
            main.startLifetime = 7.0f;
            main.startSpeed = 0.14f;
            main.startSize = new ParticleSystem.MinMaxCurve(3.6f, 5.4f);
            main.startColor = new Color(0.92f, 0.95f, 0.98f, 0.11f);
            main.maxParticles = 90;
            main.prewarm = true;
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxSize;
        }

        static void AddBoundaryFog(Transform parent, Vector3 pos, Vector3 boxSize, Material fogMat)
        {
            var go = new GameObject("BoundaryFogBank");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (psr != null && fogMat != null) psr.sharedMaterial = fogMat;
            var main = ps.main;
            main.startLifetime = 4.5f;
            main.startSpeed = 0.10f;
            main.startSize = new ParticleSystem.MinMaxCurve(2.8f, 4.4f);
            main.startColor = new Color(0.94f, 0.96f, 0.99f, 0.48f);
            main.maxParticles = 110;
            main.prewarm = true;
            var emission = ps.emission;
            emission.rateOverTime = 24f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxSize;
        }

        static GameObject EnsureBrassSiphonPrefab(string customDir)
        {
            string prefabPath = customDir + "/BrassExtractionSiphon.prefab";
            var urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material MakeMat(string fileName, Color col, float metallic, float smoothness, Color? emission = null)
            {
                string matPath = customDir + "/" + fileName;
                if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                    AssetDatabase.DeleteAsset(matPath);
                var m = new Material(urpLit) { color = col };
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
                if (emission.HasValue && m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", emission.Value);
                }
                AssetDatabase.CreateAsset(m, matPath);
                return m;
            }

            var brassMat = MakeMat("SiphonBrass.mat", new Color(0.74f, 0.53f, 0.21f), 0.78f, 0.42f);
            var ironMat = MakeMat("SiphonDarkIron.mat", new Color(0.20f, 0.21f, 0.24f), 0.72f, 0.28f);
            var manaMat = MakeMat("SiphonManaGlow.mat", new Color(0.22f, 0.88f, 0.58f), 0.1f, 0.75f, new Color(0.18f, 0.78f, 0.48f) * 1.4f);

            var root = new GameObject("BrassExtractionSiphon");
            void AddPart(string partName, PrimitiveType type, Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat)
            {
                var p = GameObject.CreatePrimitive(type);
                p.name = partName;
                Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.SetParent(root.transform, false);
                p.transform.localPosition = localPos;
                p.transform.localEulerAngles = localEuler;
                p.transform.localScale = localScale;
                p.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }

            // Grounded base flange & tap spike
            AddPart("BaseFlange", PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(0.62f, 0.05f, 0.62f), ironMat);
            AddPart("TapCollar", PrimitiveType.Cylinder, new Vector3(0f, 0.17f, 0f), Vector3.zero, new Vector3(0.36f, 0.10f, 0.36f), ironMat);

            // Pressurized brass canister + iron bands + glowing mana sight-glass
            AddPart("MainCanister", PrimitiveType.Cylinder, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(0.44f, 0.29f, 0.44f), brassMat);
            AddPart("LowerBand", PrimitiveType.Cylinder, new Vector3(0f, 0.36f, 0f), Vector3.zero, new Vector3(0.48f, 0.035f, 0.48f), ironMat);
            AddPart("UpperBand", PrimitiveType.Cylinder, new Vector3(0f, 0.74f, 0f), Vector3.zero, new Vector3(0.48f, 0.035f, 0.48f), ironMat);
            AddPart("ManaSightGlass", PrimitiveType.Cylinder, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(0.455f, 0.10f, 0.455f), manaMat);

            // Top dome & pressure valve wheel
            AddPart("TopDome", PrimitiveType.Sphere, new Vector3(0f, 0.84f, 0f), Vector3.zero, new Vector3(0.42f, 0.22f, 0.42f), brassMat);
            AddPart("ValveStem", PrimitiveType.Cylinder, new Vector3(0f, 0.98f, 0f), Vector3.zero, new Vector3(0.08f, 0.08f, 0.08f), ironMat);
            AddPart("ValveWheel", PrimitiveType.Cylinder, new Vector3(0f, 1.07f, 0f), Vector3.zero, new Vector3(0.24f, 0.025f, 0.24f), ironMat);

            // Front pressure gauge
            AddPart("GaugeNeck", PrimitiveType.Cylinder, new Vector3(0f, 0.74f, -0.24f), new Vector3(90f, 0f, 0f), new Vector3(0.07f, 0.06f, 0.07f), brassMat);
            AddPart("GaugeDial", PrimitiveType.Cylinder, new Vector3(0f, 0.74f, -0.30f), new Vector3(90f, 0f, 0f), new Vector3(0.16f, 0.025f, 0.16f), ironMat);

            // Root intake conduit extending horizontally from tank and plunging straight into the Elder Tree root (+Z)
            AddPart("ConduitUpper", PrimitiveType.Cylinder, new Vector3(0f, 0.68f, 0.36f), new Vector3(90f, 0f, 0f), new Vector3(0.11f, 0.16f, 0.11f), brassMat);
            AddPart("ConduitElbow", PrimitiveType.Sphere, new Vector3(0f, 0.68f, 0.52f), Vector3.zero, new Vector3(0.15f, 0.15f, 0.15f), ironMat);
            AddPart("ConduitRootPipe", PrimitiveType.Cylinder, new Vector3(0f, 0.38f, 0.52f), Vector3.zero, new Vector3(0.11f, 0.30f, 0.11f), brassMat);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Material EnsureRitualCircleMaterial(string customDir)
        {
            const int size = 512;
            string pngPath = customDir + "/RitualSpellCircle.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            Color glowCol = new Color(0.85f, 0.25f, 0.95f, 1f);
            Color coreCol = new Color(1.0f, 0.45f, 0.85f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);

                    float outerRing = Mathf.Exp(-Mathf.Pow((r - 0.90f) / 0.018f, 2f));
                    float midRing = Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.012f, 2f));
                    float innerRing = Mathf.Exp(-Mathf.Pow((r - 0.42f) / 0.014f, 2f));

                    // Six-pointed ritual star lines (two overlapping triangles)
                    float hex = 0f;
                    for (int k = 0; k < 6; k++)
                    {
                        float a1 = k * Mathf.PI / 3f;
                        float a2 = (k + 2) * Mathf.PI / 3f;
                        Vector2 p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * 0.78f;
                        Vector2 p2 = new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * 0.78f;
                        Vector2 p = new Vector2(dx, dy);
                        Vector2 seg = p2 - p1;
                        float t = Mathf.Clamp01(Vector2.Dot(p - p1, seg) / seg.sqrMagnitude);
                        float dist = Vector2.Distance(p, p1 + seg * t);
                        hex = Mathf.Max(hex, Mathf.Exp(-Mathf.Pow(dist / 0.012f, 2f)));
                    }

                    // Runic radial ticks between outer and mid rings
                    float tickAngle = Mathf.Abs(Mathf.Sin(ang * 12f));
                    float ticks = (r > 0.79f && r < 0.89f && tickAngle > 0.94f) ? 0.85f : 0f;

                    float alpha = Mathf.Clamp01(Mathf.Max(Mathf.Max(outerRing, midRing), Mathf.Max(innerRing, Mathf.Max(hex, ticks))));
                    Color c = Color.Lerp(glowCol, coreCol, Mathf.Clamp01(hex * 0.7f + innerRing * 0.5f));
                    c.a = alpha * 0.92f;
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            System.IO.File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            var circleTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);

            string matPath = customDir + "/RitualSpellCircle.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                AssetDatabase.DeleteAsset(matPath);

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.mainTexture = circleTex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", circleTex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 0f);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.22f);
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = 2450;
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        // --- Iron Hollow Industrial Facility Helpers -------------------------

        struct IronHollowAssetSet
        {
            public Material ironDarkMat, ironWeatheredMat, copperMat, emberMat, coalMat, ingotMat, slagMat;
            public GameObject blastFurnacePrefab, oreCrusherPrefab, refiningVatPrefab, valveManifoldPrefab, conduitJunctionPrefab, blastDoorPrefab;
        }

        static IronHollowAssetSet EnsureIronHollowAssets(string customDir)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material MakeMat(string fileName, Color col, float metallic, float smoothness, Color? emission = null)
            {
                string matPath = customDir + "/" + fileName;
                if (AssetDatabase.LoadAssetAtPath<Material>(matPath) != null)
                    AssetDatabase.DeleteAsset(matPath);
                var m = new Material(urpLit) { color = col };
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
                if (emission.HasValue && m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", emission.Value);
                }
                AssetDatabase.CreateAsset(m, matPath);
                return m;
            }

            var s = new IronHollowAssetSet
            {
                ironDarkMat = MakeMat("IH_IronDark.mat", new Color(0.18f, 0.19f, 0.22f), 0.76f, 0.28f),
                ironWeatheredMat = MakeMat("IH_IronWeathered.mat", new Color(0.30f, 0.27f, 0.25f), 0.65f, 0.22f),
                copperMat = MakeMat("IH_CopperPipe.mat", new Color(0.62f, 0.37f, 0.20f), 0.78f, 0.38f),
                emberMat = MakeMat("IH_FurnaceEmber.mat", new Color(1.0f, 0.38f, 0.08f), 0.05f, 0.2f, new Color(1.0f, 0.32f, 0.04f) * 1.6f),
                coalMat = MakeMat("IH_Coal.mat", new Color(0.10f, 0.10f, 0.12f), 0.15f, 0.18f),
                ingotMat = MakeMat("IH_IngotSteel.mat", new Color(0.55f, 0.58f, 0.62f), 0.85f, 0.52f),
                slagMat = MakeMat("IH_SolventSlag.mat", new Color(0.45f, 0.20f, 0.62f), 0.2f, 0.65f, new Color(0.35f, 0.12f, 0.52f) * 0.8f)
            };

            GameObject SavePrefab(string name, System.Action<GameObject> buildParts)
            {
                string pPath = customDir + "/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(pPath) != null)
                    AssetDatabase.DeleteAsset(pPath);
                var root = new GameObject(name);
                buildParts(root);
                var pf = PrefabUtility.SaveAsPrefabAsset(root, pPath);
                Object.DestroyImmediate(root);
                return pf;
            }

            void Part(GameObject parent, string n, PrimitiveType t, Vector3 lp, Vector3 le, Vector3 ls, Material m)
            {
                var p = GameObject.CreatePrimitive(t);
                p.name = n;
                Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.SetParent(parent.transform, false);
                p.transform.localPosition = lp;
                p.transform.localEulerAngles = le;
                p.transform.localScale = ls;
                p.GetComponent<MeshRenderer>().sharedMaterial = m;
            }

            // 1. Industrial Blast Furnace (stone/iron hearth, glowing firebox, heavy exhaust stack to ceiling)
            s.blastFurnacePrefab = SavePrefab("IH_BlastFurnace", r =>
            {
                Part(r, "HearthBase", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(2.2f, 1.1f, 2.0f), s.ironWeatheredMat);
                Part(r, "FireboxEmbers", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0.88f), Vector3.zero, new Vector3(1.1f, 0.55f, 0.3f), s.emberMat);
                Part(r, "FireboxFrame", PrimitiveType.Cube, new Vector3(0f, 0.85f, 0.96f), Vector3.zero, new Vector3(1.3f, 0.18f, 0.2f), s.ironDarkMat);
                Part(r, "SmelterDrum", PrimitiveType.Cylinder, new Vector3(0f, 1.65f, -0.1f), Vector3.zero, new Vector3(1.6f, 0.65f, 1.6f), s.ironDarkMat);
                Part(r, "ExhaustStack", PrimitiveType.Cylinder, new Vector3(0f, 3.05f, -0.25f), Vector3.zero, new Vector3(0.75f, 1.0f, 0.75f), s.ironDarkMat);
                Part(r, "LoadingHopper", PrimitiveType.Cube, new Vector3(0f, 1.45f, 0.85f), new Vector3(25f, 0f, 0f), new Vector3(0.95f, 0.45f, 0.65f), s.ironWeatheredMat);
            });

            // 2. Mechanical Ore Crusher (iron frame, top hopper, crushing block, side flywheel)
            s.oreCrusherPrefab = SavePrefab("IH_OreCrusher", r =>
            {
                Part(r, "BasePlate", PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(2.2f, 0.2f, 1.8f), s.ironDarkMat);
                Part(r, "FrameL", PrimitiveType.Cube, new Vector3(-0.85f, 1.05f, 0f), Vector3.zero, new Vector3(0.25f, 1.8f, 1.5f), s.ironDarkMat);
                Part(r, "FrameR", PrimitiveType.Cube, new Vector3(0.85f, 1.05f, 0f), Vector3.zero, new Vector3(0.25f, 1.8f, 1.5f), s.ironDarkMat);
                Part(r, "CrusherHead", PrimitiveType.Cube, new Vector3(0f, 0.85f, 0f), Vector3.zero, new Vector3(1.35f, 0.65f, 1.15f), s.ironWeatheredMat);
                Part(r, "TopHopper", PrimitiveType.Cube, new Vector3(0f, 1.75f, 0f), Vector3.zero, new Vector3(1.55f, 0.55f, 1.35f), s.copperMat);
                Part(r, "Flywheel", PrimitiveType.Cylinder, new Vector3(1.1f, 1.05f, 0f), new Vector3(0f, 0f, 90f), new Vector3(1.25f, 0.12f, 1.25f), s.ironWeatheredMat);
                Part(r, "DriveAxle", PrimitiveType.Cylinder, new Vector3(0f, 1.05f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.22f, 1.25f, 0.22f), s.ironDarkMat);
            });

            // 3. Refining Vat (riveted iron vat with copper rim & top condenser)
            s.refiningVatPrefab = SavePrefab("IH_RefiningVat", r =>
            {
                Part(r, "VatStand", PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(1.7f, 0.15f, 1.7f), s.ironWeatheredMat);
                Part(r, "VatBody", PrimitiveType.Cylinder, new Vector3(0f, 0.85f, 0f), Vector3.zero, new Vector3(1.45f, 0.65f, 1.45f), s.ironDarkMat);
                Part(r, "VatBand", PrimitiveType.Cylinder, new Vector3(0f, 1.2f, 0f), Vector3.zero, new Vector3(1.54f, 0.08f, 1.54f), s.copperMat);
                Part(r, "CondenserDome", PrimitiveType.Sphere, new Vector3(0f, 1.5f, 0f), Vector3.zero, new Vector3(1.1f, 0.45f, 1.1f), s.copperMat);
            });

            // 4. Tampered Pneumatic Valve Manifold (C1_04)
            s.valveManifoldPrefab = SavePrefab("IH_ValveManifold", r =>
            {
                Part(r, "Pedestal", PrimitiveType.Cube, new Vector3(0f, 0.4f, 0f), Vector3.zero, new Vector3(0.75f, 0.8f, 0.55f), s.ironDarkMat);
                Part(r, "ManifoldHeader", PrimitiveType.Cylinder, new Vector3(0f, 1.05f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.24f, 0.55f, 0.24f), s.copperMat);
                Part(r, "ValveStemL", PrimitiveType.Cylinder, new Vector3(-0.32f, 1.28f, 0f), Vector3.zero, new Vector3(0.08f, 0.18f, 0.08f), s.ironDarkMat);
                Part(r, "ValveWheelL", PrimitiveType.Cylinder, new Vector3(-0.32f, 1.44f, 0f), Vector3.zero, new Vector3(0.32f, 0.04f, 0.32f), s.copperMat);
                Part(r, "ValveStemR", PrimitiveType.Cylinder, new Vector3(0.32f, 1.28f, 0f), Vector3.zero, new Vector3(0.08f, 0.18f, 0.08f), s.ironDarkMat);
                Part(r, "ValveWheelR", PrimitiveType.Cylinder, new Vector3(0.32f, 1.44f, 0f), Vector3.zero, new Vector3(0.32f, 0.04f, 0.32f), s.copperMat);
                Part(r, "WedgePin", PrimitiveType.Cube, new Vector3(0.32f, 1.32f, 0.12f), new Vector3(25f, 0f, 35f), new Vector3(0.06f, 0.22f, 0.06f), s.ingotMat);
                Part(r, "PressureGauge", PrimitiveType.Cylinder, new Vector3(0f, 1.32f, -0.14f), new Vector3(90f, 0f, 0f), new Vector3(0.20f, 0.04f, 0.20f), s.ironWeatheredMat);
            });

            // 5. Dissolved Bypass Conduit Junction (C2_04)
            s.conduitJunctionPrefab = SavePrefab("IH_ConduitJunction", r =>
            {
                Part(r, "JunctionHousing", PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), Vector3.zero, new Vector3(1.35f, 0.45f, 0.85f), s.ironDarkMat);
                Part(r, "CorrodedPipeA", PrimitiveType.Cylinder, new Vector3(-0.35f, 0.50f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.14f, 0.35f, 0.14f), s.copperMat);
                Part(r, "CorrodedPipeB", PrimitiveType.Cylinder, new Vector3(0.35f, 0.50f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.14f, 0.35f, 0.14f), s.copperMat);
                Part(r, "SolventSlagPool", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), Vector3.zero, new Vector3(1.65f, 0.03f, 1.15f), s.slagMat);
            });

            // 6. Reinforced Titanium-Alloy Sub-Lab Blast Door (C3_04)
            s.blastDoorPrefab = SavePrefab("IH_BlastDoor", r =>
            {
                Part(r, "DoorSlab", PrimitiveType.Cube, new Vector3(0.75f, 1.15f, 0f), Vector3.zero, new Vector3(1.5f, 2.3f, 0.18f), s.ironDarkMat);
                Part(r, "RibTop", PrimitiveType.Cube, new Vector3(0.75f, 1.95f, 0f), Vector3.zero, new Vector3(1.54f, 0.16f, 0.24f), s.ingotMat);
                Part(r, "RibMid", PrimitiveType.Cube, new Vector3(0.75f, 1.15f, 0f), Vector3.zero, new Vector3(1.54f, 0.18f, 0.24f), s.ingotMat);
                Part(r, "RibBot", PrimitiveType.Cube, new Vector3(0.75f, 0.35f, 0f), Vector3.zero, new Vector3(1.54f, 0.16f, 0.24f), s.ingotMat);
                Part(r, "LockHousing", PrimitiveType.Cube, new Vector3(1.25f, 1.15f, 0f), Vector3.zero, new Vector3(0.32f, 0.42f, 0.28f), s.ironWeatheredMat);
            });

            return s;
        }

        static void BuildPipeRun(Transform parent, string name, Vector3 a, Vector3 b, float radius, Material pipeMat, Material flangeMat)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            Vector3 diff = b - a;
            float len = diff.magnitude;
            if (len < 0.05f) return;

            var seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seg.name = "Segment";
            Object.DestroyImmediate(seg.GetComponent<Collider>());
            seg.transform.SetParent(root, false);
            seg.transform.position = (a + b) * 0.5f;
            seg.transform.up = diff.normalized;
            seg.transform.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
            seg.GetComponent<MeshRenderer>().sharedMaterial = pipeMat;

            foreach (var endPos in new[] { a, b })
            {
                var flange = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                flange.name = "Flange";
                Object.DestroyImmediate(flange.GetComponent<Collider>());
                flange.transform.SetParent(root, false);
                flange.transform.position = endPos;
                flange.transform.up = diff.normalized;
                flange.transform.localScale = new Vector3(radius * 3.0f, 0.06f, radius * 3.0f);
                flange.GetComponent<MeshRenderer>().sharedMaterial = flangeMat;
            }
        }

        static void CreateCoalOrOrePile(string name, Vector3 pos, Material mat, Transform parent)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < 9; i++)
            {
                var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chunk.name = "Chunk_" + i;
                Object.DestroyImmediate(chunk.GetComponent<Collider>());
                chunk.transform.SetParent(root, false);
                float a = i * 0.7f;
                float r = (i == 0) ? 0f : 0.35f + (float)rng.NextDouble() * 0.45f;
                float h = (i < 3) ? 0.35f : 0.18f;
                chunk.transform.localPosition = new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r);
                chunk.transform.localEulerAngles = new Vector3(rng.Next(45), rng.Next(360), rng.Next(45));
                float sc = 0.38f + (float)rng.NextDouble() * 0.28f;
                chunk.transform.localScale = new Vector3(sc * 1.2f, sc * 0.8f, sc);
                chunk.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        static void CreateIngotStack(string name, Vector3 pos, Material ingotMat, Material palletMat, Transform parent)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;
            var basePlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basePlate.name = "IronPallet";
            Object.DestroyImmediate(basePlate.GetComponent<Collider>());
            basePlate.transform.SetParent(root, false);
            basePlate.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            basePlate.transform.localScale = new Vector3(1.5f, 0.12f, 1.1f);
            basePlate.GetComponent<MeshRenderer>().sharedMaterial = palletMat;

            for (int row = 0; row < 2; row++)
            for (int i = 0; i < 4; i++)
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = $"Ingot_{row}_{i}";
                Object.DestroyImmediate(bar.GetComponent<Collider>());
                bar.transform.SetParent(root, false);
                bar.transform.localPosition = new Vector3(-0.42f + i * 0.28f, 0.19f + row * 0.14f, 0f);
                bar.transform.localScale = new Vector3(0.22f, 0.12f, 0.65f);
                bar.GetComponent<MeshRenderer>().sharedMaterial = ingotMat;
            }
        }
    }
}
