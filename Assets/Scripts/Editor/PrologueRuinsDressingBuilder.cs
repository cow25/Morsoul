using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Editor
{
    public static class PrologueRuinsDressingBuilder
    {
        const string ScenePath = "Assets/Scenes/3D/PrologueRuins.unity";
        const string LpdRoot = "Assets/[Import] LowPolyDungeonFree/Prefabs";
        const string MfpRoot = "Assets/Mega Fantasy Props Pack/Prefabs";
        const string MtpRoot = "Assets/MedievalTavernPack/Prefabs";
        const string DfkRoot = "Assets/RunemarkStudio/DarkFantasyKit [Free]/Prefabs";
        const string Custom3DRoot = "Assets/Art/Environments/Custom/Ruins3D";

        const float FloorBaseY = 0.49f;
        const float FloorTopY = 0.69f;

        static Material _vineStemMat;
        static Material _vineLeafMat;
        static Material _dirtMat;
        static Material _sunrayMat;

        static Mesh _vineHangingMesh;
        static Mesh _vineCreepMesh;
        static Mesh _dirtMoundMesh;
        static Mesh _sunrayMesh;

        static GameObject _ceilHoleCenterPrefab;
        static GameObject _ceilHoleMultiPrefab;
        static GameObject _ceilHoleCornerPrefab;

        static GameObject _wallHoleCenterPrefab;
        static GameObject _wallTiltedFissurePrefab;
        static GameObject _wallDualBreachPrefab;
        static GameObject _wallTopCornerPrefab;

        [MenuItem("Morsoul/3D/Rebuild Prologue Ruins Dressing")]
        public static void Rebuild()
        {
            var scene = SceneManager.GetActiveScene();
            bool openedHere = false;
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                openedHere = true;
            }

            EnsureCustomAssets();

            var room = FindRoot(scene, "Room");
            if (room != null)
            {
                var mr = room.GetComponent<MeshRenderer>();
                if (mr != null) mr.enabled = false;
            }

            foreach (var hideName in new[] { "Cube (1)", "Sample Light Source 1", "Sample Light Source 2", "FireLight (1)", "FireLight (3)" })
            {
                var go = FindRoot(scene, hideName);
                if (go != null) go.SetActive(false);
            }

            var oldDressing = FindRoot(scene, "RuinsDressing");
            if (oldDressing != null) Object.DestroyImmediate(oldDressing);

            var root = new GameObject("RuinsDressing");
            SceneManager.MoveGameObjectToScene(root, scene);

            var archRoot = new GameObject("Architecture").transform;
            archRoot.SetParent(root.transform, false);
            var propsRoot = new GameObject("ResearchFacilityProps").transform;
            propsRoot.SetParent(root.transform, false);
            var natureRoot = new GameObject("OvergrowthAndDirt").transform;
            natureRoot.SetParent(root.transform, false);
            var lightsRoot = new GameObject("SunlightAndAmbient").transform;
            lightsRoot.SetParent(root.transform, false);

            BuildFloors(archRoot);
            BuildSealedAndBrokenWalls(archRoot);
            BuildGothicColonnadeAndRoof(archRoot);
            BuildResearchFacilityProps(propsRoot);
            BuildOvergrowthAndVines(natureRoot);
            BuildSubterraneanSunlight(lightsRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (openedHere) AssetDatabase.SaveAssets();
            Debug.Log("PrologueRuinsDressingBuilder: rebuilt gothic ruined research facility.");
        }

        static void BuildFloors(Transform parent)
        {
            var worn = LoadLpd("02_Floors/SM_LPD_Floor_PavingFlagstoneWorn_01.prefab");
            var cracked = LoadLpd("02_Floors/SM_LPD_Floor_PavingCracked_01.prefab");
            var dirt = LoadLpd("02_Floors/SM_LPD_Floor_PavingDirt_01.prefab");
            var cobble = LoadLpd("02_Floors/SM_LPD_Floor_PavingCobble_01.prefab");

            var rng = new System.Random(107);
            for (int gx = -18; gx <= 18; gx += 4)
            {
                for (int gz = -8; gz <= 20; gz += 4)
                {
                    bool underSunHole =
                        (Mathf.Abs(gx - (-2)) <= 2 && Mathf.Abs(gz - 4) <= 2) ||
                        (Mathf.Abs(gx - 14) <= 2 && Mathf.Abs(gz - 8) <= 2) ||
                        (Mathf.Abs(gx - 2) <= 2 && Mathf.Abs(gz - (-4)) <= 2);

                    GameObject prefab;
                    if (underSunHole)
                        prefab = rng.Next(100) < 65 ? dirt : cracked;
                    else
                    {
                        int r = rng.Next(100);
                        prefab = r < 38 ? cracked : (r < 68 ? worn : (r < 85 ? dirt : cobble));
                    }
                    int rotY = rng.Next(4) * 90;
                    Place(prefab, $"Floor_{gx}_{gz}", parent, new Vector3(gx, FloorBaseY, gz), new Vector3(0f, rotY, 0f));
                }
            }
        }

        static void BuildSealedAndBrokenWalls(Transform parent)
        {
            var ashlar = LoadLpd("01_Structure/SM_LPD_Struct_WallAshlar_01.prefab");
            var coursed = LoadLpd("01_Structure/SM_LPD_Struct_WallCoursed_01.prefab");
            var cracked = LoadLpd("01_Structure/SM_LPD_Struct_WallCracked_01.prefab");
            var rubbleWall = LoadLpd("01_Structure/SM_LPD_Struct_WallRubble_01.prefab");
            var wallRuined = LoadLpd("01_Structure/SM_LPD_Struct_WallRuined_01.prefab");
            var wallRuinHalf = LoadLpd("01_Structure/SM_LPD_Struct_WallRuinHalf_01.prefab");
            var wallNiche = LoadLpd("01_Structure/SM_LPD_Struct_WallNiche_01.prefab");
            var wallArch = LoadLpd("01_Structure/SM_LPD_Struct_WallArch_01.prefab");
            var wallSlit = LoadLpd("01_Structure/SM_LPD_Struct_WallSlit_01.prefab");
            var wallLancet = LoadLpd("01_Structure/SM_LPD_Struct_WallWindowLancet_01.prefab");
            var corner = LoadLpd("01_Structure/SM_LPD_Struct_CornerPostQuoined_01.prefab");

            var rng = new System.Random(203);
            for (int gz = -8; gz <= 20; gz += 4)
            {
                var wBot = rng.Next(100) < 55 ? cracked : (rng.Next(2) == 0 ? rubbleWall : ashlar);
                var eBot = rng.Next(100) < 55 ? cracked : (rng.Next(2) == 0 ? rubbleWall : ashlar);
                Place(wBot, $"WallW_Bot_{gz}", parent, new Vector3(-19.8f, FloorTopY, gz), new Vector3(0f, 90f, 0f));
                // High-up broken wall variations letting outside daylight leak through while ground level stays sealed
                GameObject wTop = coursed;
                if (gz == 0) wTop = _wallHoleCenterPrefab;
                else if (gz == 4) wTop = _wallTiltedFissurePrefab;
                else if (gz == 12) wTop = _wallDualBreachPrefab;
                else if (gz == -4) wTop = _wallTopCornerPrefab;
                Place(wTop, $"WallW_Top_{gz}", parent, new Vector3(-19.8f, FloorTopY + 4f, gz), new Vector3(0f, 90f, 0f));
                Place(ashlar, $"WallW_Cap_{gz}", parent, new Vector3(-19.8f, FloorTopY + 8f, gz), new Vector3(0f, 90f, 0f));

                Place(eBot, $"WallE_Bot_{gz}", parent, new Vector3(19.8f, FloorTopY, gz), new Vector3(0f, -90f, 0f));
                GameObject eTop = coursed;
                if (gz == 8) eTop = _wallTiltedFissurePrefab;
                else if (gz == 0) eTop = _wallHoleCenterPrefab;
                else if (gz == 12) eTop = _wallTopCornerPrefab;
                else if (gz == -4) eTop = _wallDualBreachPrefab;
                Place(eTop, $"WallE_Top_{gz}", parent, new Vector3(19.8f, FloorTopY + 4f, gz), new Vector3(0f, -90f, 0f));
                Place(ashlar, $"WallE_Cap_{gz}", parent, new Vector3(19.8f, FloorTopY + 8f, gz), new Vector3(0f, -90f, 0f));
            }

            // Inner broken masonry & gothic wall relief seated flush on floor (adds ruined depth without exposing outside)
            Place(wallNiche, "WallW_Niche_0", parent, new Vector3(-19.25f, FloorTopY, 6.6f), new Vector3(0f, 90f, 0f));
            Place(wallRuined, "WallW_Ruined_S", parent, new Vector3(-19.25f, FloorTopY, -4.0f), new Vector3(0f, 90f, 0f));
            Place(wallRuinHalf, "WallW_RuinHalf_N", parent, new Vector3(-19.25f, FloorTopY, 15.5f), new Vector3(0f, 90f, 0f));
            Place(wallArch, "WallE_Arch_N", parent, new Vector3(19.25f, FloorTopY, 8.0f), new Vector3(0f, -90f, 0f));
            Place(wallRuined, "WallE_Ruined_S", parent, new Vector3(19.25f, FloorTopY, -3.5f), new Vector3(0f, -90f, 0f));
            Place(wallRuinHalf, "WallE_RuinHalf_N", parent, new Vector3(19.25f, FloorTopY, 15.0f), new Vector3(0f, -90f, 0f));

            for (int gx = -18; gx <= 18; gx += 4)
            {
                var bot = rng.Next(100) < 60 ? cracked : rubbleWall;
                Place(bot, $"WallS_Bot_{gx}", parent, new Vector3(gx, FloorTopY, -10.0f), Vector3.zero);
                Place(coursed, $"WallS_Top_{gx}", parent, new Vector3(gx, FloorTopY + 4f, -10.0f), Vector3.zero);
                Place(ashlar, $"WallS_Cap_{gx}", parent, new Vector3(gx, FloorTopY + 8f, -10.0f), Vector3.zero);
            }

            foreach (int gx in new[] { -18, -14, -10, 10, 14, 18 })
            {
                var bot = Mathf.Abs(gx) == 14 ? rubbleWall : cracked;
                Place(bot, $"WallN_Bot_{gx}", parent, new Vector3(gx, FloorTopY, 18.8f), new Vector3(0f, 180f, 0f));
                var nTop = gx == -14 ? _wallHoleCenterPrefab : (gx == 14 ? _wallTiltedFissurePrefab : coursed);
                Place(nTop, $"WallN_Top_{gx}", parent, new Vector3(gx, FloorTopY + 4f, 18.8f), new Vector3(0f, 180f, 0f));
                Place(ashlar, $"WallN_Cap_{gx}", parent, new Vector3(gx, FloorTopY + 8f, 18.8f), new Vector3(0f, 180f, 0f));
            }
            Place(wallRuinHalf, "WallN_Ruined_W", parent, new Vector3(-14.0f, FloorTopY, 18.25f), new Vector3(0f, 180f, 0f));
            Place(wallRuinHalf, "WallN_Ruined_E", parent, new Vector3(14.0f, FloorTopY, 18.25f), new Vector3(0f, 180f, 0f));

            for (int gx = -6; gx <= 6; gx += 4)
            {
                Place(ashlar, $"GateSealBack_Bot_{gx}", parent, new Vector3(gx, FloorTopY, 19.7f), new Vector3(0f, 180f, 0f));
                Place(coursed, $"GateSealBack_Top_{gx}", parent, new Vector3(gx, FloorTopY + 4f, 19.7f), new Vector3(0f, 180f, 0f));
                Place(ashlar, $"GateSealBack_Cap_{gx}", parent, new Vector3(gx, FloorTopY + 8f, 19.7f), new Vector3(0f, 180f, 0f));
            }

            Place(corner, "GateFrame_L", parent, new Vector3(-10.0f, FloorTopY, 18.75f), Vector3.zero, new Vector3(1.5f, 2.8f, 1.5f));
            Place(corner, "GateFrame_R", parent, new Vector3(10.05f, FloorTopY, 18.75f), Vector3.zero, new Vector3(1.5f, 2.8f, 1.5f));
            Place(corner, "Corner_NW", parent, new Vector3(-19.8f, FloorTopY, 18.8f), Vector3.zero, new Vector3(1.5f, 3.0f, 1.5f));
            Place(corner, "Corner_NE", parent, new Vector3(19.8f, FloorTopY, 18.8f), Vector3.zero, new Vector3(1.5f, 3.0f, 1.5f));
            Place(corner, "Corner_SW", parent, new Vector3(-19.8f, FloorTopY, -10.0f), Vector3.zero, new Vector3(1.5f, 3.0f, 1.5f));
            Place(corner, "Corner_SE", parent, new Vector3(19.8f, FloorTopY, -10.0f), Vector3.zero, new Vector3(1.5f, 3.0f, 1.5f));
        }

        static void BuildGothicColonnadeAndRoof(Transform parent)
        {
            var pillarOrnate = LoadLpd("01_Structure/SM_LPD_Struct_PillarOrnate_01.prefab");
            var pillarSquare = LoadLpd("01_Structure/SM_LPD_Struct_PillarSquare_01.prefab");
            var pillarBroken = LoadLpd("01_Structure/SM_LPD_Struct_PillarBroken_01.prefab");
            var archFree = LoadLpd("01_Structure/SM_LPD_Struct_ArchFree_01.prefab");
            var ceilGroin = LoadLpd("01_Structure/SM_LPD_Struct_CeilGroinRibbed_01.prefab");
            var ceilVaultRib = LoadLpd("01_Structure/SM_LPD_Struct_CeilVaultBayRib_01.prefab");
            var ceilFlat = LoadLpd("01_Structure/SM_LPD_Struct_CeilFlat_01.prefab");
            var stairHalf = LoadLpd("01_Structure/SM_LPD_Struct_StairHalfFlight_01.prefab");
            var rubbleMound = LoadLpd("01_Structure/SM_LPD_Struct_RubbleHeapMound_01.prefab");
            var rubbleAgainst = LoadLpd("01_Structure/SM_LPD_Struct_RubbleHeapAgainst_01.prefab");
            var debris = LoadLpd("01_Structure/SM_LPD_Struct_DebrisScatter_01.prefab");
            var ashlar = LoadLpd("01_Structure/SM_LPD_Struct_WallAshlar_01.prefab");
            var brickA = LoadMfp("Miscellaneous/Bricks/brick.001.prefab");
            var brickB = LoadMfp("Miscellaneous/Bricks/brick.003.prefab");
            var stoneCol = LoadMfp("Columns/stone_column.prefab");

            // Gothic colonnade pillars every 4m + overhead arches resting squarely on pillar capitals (broken bays left collapsed)
            foreach (float pz in new[] { -4f, 8f, 12f, 16f })
            {
                Place(pillarOrnate, $"Pillar_L_Bot_{pz}", parent, new Vector3(-7.0f, FloorTopY, pz), Vector3.zero, new Vector3(1.25f, 1.25f, 1.25f));
                Place(pillarSquare, $"Pillar_L_Top_{pz}", parent, new Vector3(-7.0f, FloorTopY + 5.0f, pz), Vector3.zero, new Vector3(1.25f, 1.0f, 1.25f));
            }
            Place(pillarBroken, "Pillar_L_Broken_0", parent, new Vector3(-7.0f, FloorTopY, 0.0f), new Vector3(0f, 25f, 0f), new Vector3(1.1f, 1.1f, 1.1f));
            Place(pillarBroken, "Pillar_L_Broken_4", parent, new Vector3(-7.0f, FloorTopY, 4.0f), new Vector3(0f, -40f, 0f), new Vector3(1.1f, 1.1f, 1.1f));
            Place(rubbleMound, "Rubble_L_Mid", parent, new Vector3(-6.4f, FloorTopY, 3.6f), new Vector3(0f, 40f, 0f));

            foreach (float pz in new[] { 4f, 8f, 12f, 16f })
            {
                Place(pillarOrnate, $"Pillar_R_Bot_{pz}", parent, new Vector3(7.0f, FloorTopY, pz), Vector3.zero, new Vector3(1.25f, 1.25f, 1.25f));
                Place(pillarSquare, $"Pillar_R_Top_{pz}", parent, new Vector3(7.0f, FloorTopY + 5.0f, pz), Vector3.zero, new Vector3(1.25f, 1.0f, 1.25f));
            }
            Place(pillarBroken, "Pillar_R_Broken_S", parent, new Vector3(7.0f, FloorTopY, -4.0f), new Vector3(0f, -110f, 0f), new Vector3(1.1f, 1.1f, 1.1f));
            Place(pillarBroken, "Pillar_R_Broken_0", parent, new Vector3(7.0f, FloorTopY, 0.0f), new Vector3(0f, 75f, 0f), new Vector3(1.1f, 1.1f, 1.1f));
            Place(debris, "Debris_R_South", parent, new Vector3(6.2f, FloorTopY, -3.2f), new Vector3(0f, 15f, 0f));

            // 4m arches spanning intact pillar-to-pillar bays (zero floating legs)
            Place(archFree, "Arch_L_10", parent, new Vector3(-7.0f, FloorTopY + 4.2f, 10.0f), new Vector3(0f, 90f, 0f));
            Place(archFree, "Arch_L_14", parent, new Vector3(-7.0f, FloorTopY + 4.2f, 14.0f), new Vector3(0f, 90f, 0f));
            Place(archFree, "Arch_R_6", parent, new Vector3(7.0f, FloorTopY + 4.2f, 6.0f), new Vector3(0f, 90f, 0f));
            Place(archFree, "Arch_R_10", parent, new Vector3(7.0f, FloorTopY + 4.2f, 10.0f), new Vector3(0f, 90f, 0f));
            Place(archFree, "Arch_R_14", parent, new Vector3(7.0f, FloorTopY + 4.2f, 14.0f), new Vector3(0f, 90f, 0f));

            // Fallen masonry chunks & rubble beneath collapsed ceiling/wall bays
            Place(stoneCol, "FallenColumn_W", parent, new Vector3(-4.8f, FloorTopY + 0.35f, 1.8f), new Vector3(82f, 35f, 10f), new Vector3(0.45f, 0.45f, 0.45f));
            Place(brickA, "FallenBrick_1", parent, new Vector3(-1.4f, FloorTopY + 0.1f, 3.6f), new Vector3(12f, 40f, 8f));
            Place(brickB, "FallenBrick_2", parent, new Vector3(14.3f, FloorTopY + 0.1f, 7.6f), new Vector3(-8f, 115f, 14f));
            Place(brickA, "FallenBrick_3", parent, new Vector3(2.3f, FloorTopY + 0.1f, -3.5f), new Vector3(18f, -30f, 5f));

            Place(rubbleAgainst, "Collapse_SW", parent, new Vector3(-17.8f, FloorTopY, -7.8f), new Vector3(0f, 90f, 0f), new Vector3(1.2f, 1.2f, 1.2f));
            Place(rubbleAgainst, "Collapse_NW", parent, new Vector3(-14.5f, FloorTopY, 16.8f), new Vector3(0f, 180f, 0f), new Vector3(1.2f, 1.2f, 1.2f));
            Place(rubbleMound, "Collapse_CenterSide", parent, new Vector3(-2.6f, FloorTopY, 9.5f), new Vector3(0f, 70f, 0f));
            Place(debris, "Debris_Center1", parent, new Vector3(1.2f, FloorTopY, 3.5f), new Vector3(0f, 22f, 0f));
            Place(debris, "Debris_Center2", parent, new Vector3(-2.5f, FloorTopY, 11.5f), new Vector3(0f, -65f, 0f));

            // Full roof extended to gz=20 so ZERO rooftop gaps exist at WallN
            for (int gx = -18; gx <= 18; gx += 4)
            {
                for (int gz = -8; gz <= 20; gz += 4)
                {
                    if (gx == -2 && gz == 4)
                    {
                        Place(_ceilHoleCenterPrefab, $"CeilHole_Center_{gx}_{gz}", parent, new Vector3(gx, FloorTopY + 8.0f, gz), Vector3.zero);
                    }
                    else if (gx == 14 && gz == 8)
                    {
                        Place(_ceilHoleMultiPrefab, $"CeilHole_Multi_{gx}_{gz}", parent, new Vector3(gx, FloorTopY + 8.0f, gz), Vector3.zero);
                    }
                    else if (gx == 2 && gz == -4)
                    {
                        Place(_ceilHoleCornerPrefab, $"CeilHole_Corner_{gx}_{gz}", parent, new Vector3(gx, FloorTopY + 8.0f, gz), Vector3.zero);
                    }
                    else
                    {
                        var c = Mathf.Abs(gx) <= 6 ? ceilGroin : ceilVaultRib;
                        Place(c, $"Ceil_{gx}_{gz}", parent, new Vector3(gx, FloorTopY + 8.0f, gz), Vector3.zero);
                        Place(ceilFlat, $"CeilCap_{gx}_{gz}", parent, new Vector3(gx, FloorTopY + 9.4f, gz), Vector3.zero);
                    }
                }
            }
        }

        static void BuildResearchFacilityProps(Transform parent)
        {
            // West Archive & Study Bay (around Desk at (-18.05, 0.69, 6.63)) - zero overlaps, zero floating
            Place(LoadDfk("Decoration/Textiles/dfk_carpet_black_small.prefab"), "Study_Carpet", parent, new Vector3(-17.4f, FloorTopY + 0.015f, 6.6f), Vector3.zero, new Vector3(1.6f, 1f, 1.8f));
            Place(LoadMfp("Chairs/chair.001.prefab"), "Study_ChairTipped", parent, new Vector3(-16.1f, FloorTopY + 0.05f, 5.9f), new Vector3(78f, -125f, 10f));
            Place(LoadMtp("Furniture/Table_01.prefab"), "Study_SideTable", parent, new Vector3(-18.2f, FloorTopY, 2.2f), new Vector3(0f, 90f, 0f));
            Place(LoadLpd("04_Lighting/SM_LPD_Light_OilLampBrass_01.prefab"), "Study_UnlitLamp", parent, new Vector3(-18.2f, FloorTopY + 0.72f, 2.0f), Vector3.zero, stripLights: true);
            Place(LoadMfp("Miscellaneous/Books/books.001.prefab"), "Study_Books1", parent, new Vector3(-18.1f, FloorTopY + 0.72f, 2.5f), new Vector3(0f, 20f, 0f));
            Place(LoadMfp("Miscellaneous/scroll.prefab"), "Study_Scroll", parent, new Vector3(-18.3f, FloorTopY + 0.72f, 2.3f), new Vector3(0f, -35f, 0f));

            Place(LoadDfk("Furnitures/dfk_shelf_01.prefab"), "Study_WallShelf", parent, new Vector3(-19.35f, FloorTopY + 1.85f, 6.6f), new Vector3(0f, 90f, 0f));
            Place(LoadMfp("Miscellaneous/On Table/bottle.001.prefab"), "Study_ShelfBottle", parent, new Vector3(-19.32f, FloorTopY + 1.99f, 6.4f), Vector3.zero);
            Place(LoadMfp("Miscellaneous/Candels/candle.001.prefab"), "Study_ShelfCandle", parent, new Vector3(-19.32f, FloorTopY + 1.99f, 6.85f), Vector3.zero, stripLights: true);

            // Spaced cupboards along West wall (NO shoring prop overlapping them!)
            Place(LoadMtp("Furniture/Cupboard_01.prefab"), "Archive_Cupboard", parent, new Vector3(-18.9f, FloorTopY, 10.8f), new Vector3(0f, 90f, 0f));
            Place(LoadDfk("Furnitures/dfk_wardrobe_01.prefab"), "Archive_Wardrobe", parent, new Vector3(-18.9f, FloorTopY, 13.0f), new Vector3(0f, 90f, 0f));
            Place(LoadMfp("Storage/gabinet.prefab"), "Archive_Cabinet", parent, new Vector3(-18.8f, FloorTopY, 15.6f), new Vector3(0f, 90f, 0f));
            Place(LoadDfk("Decoration/Flags/dfk_flag_02_a.prefab"), "Study_TatteredFlagW", parent, new Vector3(-19.45f, FloorTopY + 3.8f, 9.0f), new Vector3(0f, 90f, 0f));

            // Central-West Alchemy & Specimen Workbench (table.002 top = FloorTopY + 0.99f)
            Place(LoadMfp("Tables/table.002.prefab"), "Lab_MainTable", parent, new Vector3(-3.8f, FloorTopY, 4.2f), new Vector3(0f, 18f, 0f));
            Place(LoadMfp("Miscellaneous/Books/book_single_with_free_pages.prefab"), "Lab_OpenBook", parent, new Vector3(-3.6f, FloorTopY + 0.99f, 4.1f), new Vector3(0f, 25f, 0f));
            Place(LoadMfp("Miscellaneous/scrolls.prefab"), "Lab_Scrolls", parent, new Vector3(-4.0f, FloorTopY + 0.99f, 4.7f), new Vector3(0f, -15f, 0f));
            Place(LoadMfp("Miscellaneous/On Table/bottle.002.prefab"), "Lab_Bottle1", parent, new Vector3(-3.4f, FloorTopY + 0.99f, 3.6f), Vector3.zero);
            Place(LoadMfp("Miscellaneous/On Table/bottle.003.prefab"), "Lab_Bottle2", parent, new Vector3(-3.7f, FloorTopY + 0.99f, 3.4f), Vector3.zero);
            Place(LoadDfk("Lightsources/dfk_crystal_01_blue.prefab"), "Lab_SpecimenCrystal", parent, new Vector3(-4.1f, FloorTopY + 0.99f, 4.2f), Vector3.zero, Vector3.one * 0.65f, stripLights: true);
            Place(LoadMfp("Miscellaneous/Candels/candle.002.prefab"), "Lab_UnlitCandle", parent, new Vector3(-3.5f, FloorTopY + 0.99f, 4.8f), Vector3.zero, stripLights: true);
            Place(LoadMfp("Miscellaneous/cauldron.prefab"), "Lab_Cauldron", parent, new Vector3(-5.3f, FloorTopY, 5.4f), Vector3.zero, Vector3.one * 0.75f);
            Place(LoadDfk("Containers/Chests/dfk_chest_02_closed.prefab"), "Lab_Chest", parent, new Vector3(-5.1f, FloorTopY, 2.3f), new Vector3(0f, 35f, 0f));

            // East Research Station (Table_01 top = FloorTopY + 0.72f)
            Place(LoadMtp("Furniture/Table_01.prefab"), "East_ResearchTable", parent, new Vector3(11.5f, FloorTopY, -1.5f), new Vector3(0f, -20f, 0f));
            Place(LoadMfp("Miscellaneous/Books/books.003.prefab"), "East_Books", parent, new Vector3(11.3f, FloorTopY + 0.72f, -1.5f), new Vector3(0f, 10f, 0f));
            Place(LoadMtp("Tableware/Jug_01.prefab"), "East_Jug", parent, new Vector3(11.8f, FloorTopY + 0.72f, -1.4f), Vector3.zero);
            Place(LoadMtp("Tableware/Glass_01.prefab"), "East_Glass", parent, new Vector3(11.6f, FloorTopY + 0.72f, -1.7f), Vector3.zero);
            Place(LoadMfp("Miscellaneous/Vases/vase.001.prefab"), "East_Vase", parent, new Vector3(11.1f, FloorTopY + 0.72f, -1.3f), Vector3.zero);
            Place(LoadMfp("Chairs/bench.001.prefab"), "East_Bench", parent, new Vector3(11.5f, FloorTopY, -2.7f), new Vector3(0f, -20f, 0f));
            Place(LoadMfp("Storage/shelf.prefab"), "East_StorageShelf", parent, new Vector3(18.6f, FloorTopY, 1.5f), new Vector3(0f, -90f, 0f));
            Place(LoadDfk("Decoration/Flags/dfk_flag_02_a.prefab"), "East_TatteredFlag", parent, new Vector3(19.45f, FloorTopY + 3.8f, 11.2f), new Vector3(0f, -90f, 0f));

            // Overhead chandeliers mounted flush to vaulted ceiling (unlit)
            Place(LoadMtp("Ornaments/Chandelier_01.prefab"), "Nave_Chandelier_1", parent, new Vector3(0f, FloorTopY + 7.81f, 2.0f), Vector3.zero, Vector3.one * 2.5f, stripLights: true);
            Place(LoadMtp("Ornaments/Chandelier_01.prefab"), "Nave_Chandelier_2", parent, new Vector3(0f, FloorTopY + 7.81f, 12.0f), new Vector3(0f, 45f, 0f), Vector3.one * 2.5f, stripLights: true);

            // Unlit braziers & sconces (zero open lit fires)
            var brazierUnlit = LoadLpd("04_Lighting/SM_LPD_Light_BrazierTripodUnlit_01.prefab");
            var sconceUnlit = LoadLpd("04_Lighting/SM_LPD_Light_SconceIronUnlit_01.prefab");
            Place(brazierUnlit, "Brazier_Unlit_0", parent, new Vector3(-4.5f, FloorTopY, -3.5f), Vector3.zero, stripLights: true);
            Place(brazierUnlit, "Brazier_Tipped_1", parent, new Vector3(4.6f, FloorTopY + 0.1f, -3.2f), new Vector3(72f, 35f, 15f), stripLights: true);
            Place(brazierUnlit, "Brazier_Unlit_2", parent, new Vector3(-6.2f, FloorTopY, 15.2f), Vector3.zero, stripLights: true);
            Place(brazierUnlit, "Brazier_Unlit_3", parent, new Vector3(6.2f, FloorTopY, 15.2f), Vector3.zero, stripLights: true);
            Place(sconceUnlit, "Sconce_Study_Unlit", parent, new Vector3(-19.55f, FloorTopY + 2.6f, 6.6f), new Vector3(0f, 90f, 0f), stripLights: true);
            Place(sconceUnlit, "Sconce_EastKey_Unlit", parent, new Vector3(19.55f, FloorTopY + 2.6f, 7.6f), new Vector3(0f, -90f, 0f), stripLights: true);

            // East collapsed debris & crates (spaced clear of shelves/tables)
            Place(LoadLpd("01_Structure/SM_LPD_Struct_RubbleHeapAgainst_01.prefab"), "East_RubbleWall", parent, new Vector3(18.0f, FloorTopY, 9.8f), new Vector3(0f, -90f, 0f));
            Place(LoadLpd("03_Doors/SM_LPD_Door_DoorBroken_01.prefab"), "East_BrokenDoor", parent, new Vector3(17.5f, FloorTopY, 13.8f), new Vector3(14f, -110f, 0f));
            Place(LoadLpd("05_Containers/SM_LPD_Crate_BarrelSide_01.prefab"), "East_BarrelSide", parent, new Vector3(17.4f, FloorTopY, 4.0f), new Vector3(0f, 30f, 0f));
            Place(LoadLpd("05_Containers/SM_LPD_Crate_HandcartEmpty_01.prefab"), "South_HandcartEmpty", parent, new Vector3(-11.5f, FloorTopY, -6.5f), new Vector3(0f, 55f, 0f));
        }

        static void BuildOvergrowthAndVines(Transform parent)
        {
            var bushes = new[] {
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YughuesFreeBushes2018/Prefabs/P_Bush01.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YughuesFreeBushes2018/Prefabs/P_Bush02.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YughuesFreeBushes2018/Prefabs/P_Bush03.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YughuesFreeBushes2018/Prefabs/P_Bush04.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/YughuesFreeBushes2018/Prefabs/P_Bush05.prefab"),
            };

            Vector3[] dirtSpots = new[] {
                new Vector3(-2.0f, FloorTopY, 4.0f),
                new Vector3(14.0f, FloorTopY, 8.0f),
                new Vector3(2.0f, FloorTopY, -4.0f),
                new Vector3(-12.5f, FloorTopY, 14.8f),
                new Vector3(11.0f, FloorTopY, 15.2f),
                new Vector3(-6.2f, FloorTopY, 3.2f),
            };
            for (int i = 0; i < dirtSpots.Length; i++)
            {
                float s = 1.2f + (i % 3) * 0.4f;
                PlaceCustomMesh("DirtMound_" + i, _dirtMoundMesh, _dirtMat, parent, dirtSpots[i], new Vector3(0f, i * 47f, 0f), new Vector3(s, 0.8f + (i % 2) * 0.3f, s));
            }

            var rng = new System.Random(313);
            Vector3[] bushSpots = new[] {
                new Vector3(-2.2f, FloorTopY + 0.12f, 3.8f),
                new Vector3(-1.3f, FloorTopY + 0.05f, 4.7f),
                new Vector3(13.8f, FloorTopY + 0.12f, 8.2f),
                new Vector3(14.9f, FloorTopY + 0.05f, 7.1f),
                new Vector3(2.1f, FloorTopY + 0.12f, -3.9f),
                new Vector3(1.2f, FloorTopY + 0.04f, -4.6f),
                new Vector3(-12.6f, FloorTopY + 0.08f, 15.0f),
                new Vector3(11.2f, FloorTopY + 0.08f, 15.4f),
                new Vector3(-6.8f, FloorTopY + 0.04f, 12.2f),
                new Vector3(7.4f, FloorTopY + 0.04f, 3.6f),
                new Vector3(-16.5f, FloorTopY + 0.04f, -5.8f),
                new Vector3(16.8f, FloorTopY + 0.04f, -5.2f),
            };
            for (int i = 0; i < bushSpots.Length; i++)
            {
                var bp = bushes[i % bushes.Length];
                float sc = 0.7f + (float)rng.NextDouble() * 0.55f;
                Place(bp, $"Overgrowth_Bush_{i}", parent, bushSpots[i], new Vector3(0f, rng.Next(360), 0f), Vector3.one * sc);
            }

            Vector3[] hangSpots = new[] {
                new Vector3(-2.8f, FloorTopY + 8.0f, 3.2f),
                new Vector3(-1.2f, FloorTopY + 8.0f, 4.8f),
                new Vector3(13.2f, FloorTopY + 8.0f, 7.3f),
                new Vector3(14.8f, FloorTopY + 8.0f, 8.7f),
                new Vector3(1.3f, FloorTopY + 8.0f, -4.7f),
                new Vector3(2.8f, FloorTopY + 8.0f, -3.2f),
                new Vector3(-6.9f, FloorTopY + 8.0f, 12.2f),
                new Vector3(7.1f, FloorTopY + 8.0f, 13.8f),
                new Vector3(-9.6f, FloorTopY + 8.0f, 17.8f),
                new Vector3(9.6f, FloorTopY + 8.0f, 17.8f),
            };
            for (int i = 0; i < hangSpots.Length; i++)
            {
                float sy = 0.85f + (i % 3) * 0.25f;
                PlaceCustomMesh($"Vine_Hang_{i}", _vineHangingMesh, _vineLeafMat, parent, hangSpots[i], new Vector3(0f, i * 63f, 0f), new Vector3(1f, sy, 1f));
            }

            (Vector3 pos, float rotY, float scale)[] creepSpots = new[] {
                (new Vector3(-19.5f, FloorTopY + 1.8f, 10.0f), 90f, 1.3f),
                (new Vector3(-19.5f, FloorTopY + 2.4f, -2.0f), 90f, 1.1f),
                (new Vector3(19.5f, FloorTopY + 2.0f, 6.0f), -90f, 1.4f),
                (new Vector3(19.5f, FloorTopY + 1.5f, 12.0f), -90f, 1.2f),
                (new Vector3(-10.1f, FloorTopY + 2.2f, 18.4f), 180f, 1.3f),
                (new Vector3(10.1f, FloorTopY + 2.2f, 18.4f), 180f, 1.3f),
                (new Vector3(-7.0f, FloorTopY + 1.6f, -3.3f), 0f, 0.9f),
                (new Vector3(7.0f, FloorTopY + 1.8f, 4.7f), 180f, 0.95f),
            };
            for (int i = 0; i < creepSpots.Length; i++)
            {
                var c = creepSpots[i];
                PlaceCustomMesh($"Vine_Creep_{i}", _vineCreepMesh, _vineLeafMat, parent, c.pos, new Vector3(0f, c.rotY, 0f), Vector3.one * c.scale);
            }
        }

        static void BuildSubterraneanSunlight(Transform parent)
        {
            // Outside directional sun leaking through the broken roof holes & high wall slits
            var sunGo = new GameObject("OutsideDirectionalSun");
            sunGo.transform.SetParent(parent, false);
            sunGo.transform.eulerAngles = new Vector3(68f, 25f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.95f, 0.97f, 0.88f);
            sun.intensity = 2.4f;
            sun.shadows = LightShadows.Soft;

            Vector3[] sunTargets = new[] {
                new Vector3(-2.0f, FloorTopY, 4.0f),
                new Vector3(14.0f, FloorTopY, 8.0f),
                new Vector3(2.0f, FloorTopY, -4.0f),
            };
            for (int i = 0; i < sunTargets.Length; i++)
            {
                var t = sunTargets[i];
                var lgo = new GameObject($"SunlightShaft_{i}");
                lgo.transform.SetParent(parent, false);
                lgo.transform.position = new Vector3(t.x, FloorTopY + 11.0f, t.z);
                lgo.transform.LookAt(t + new Vector3(0.6f, 0f, 0.5f));
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = new Color(0.95f, 0.96f, 0.85f);
                l.intensity = 65f;
                l.range = 20f;
                l.spotAngle = 36f;
                l.innerSpotAngle = 14f;
                l.shadows = LightShadows.Soft;

                // Translucent volumetric sunray cone originating inside the broken ceiling vault opening
                Vector3 beamOrigin = i == 2
                    ? new Vector3(t.x + 1.0f, FloorTopY + 8.9f, t.z + 1.0f)
                    : new Vector3(t.x, FloorTopY + 9.35f, t.z);
                PlaceCustomMesh($"SunrayBeam_{i}_A", _sunrayMesh, _sunrayMat, parent, beamOrigin, new Vector3(6f, 0f, -5f), new Vector3(0.95f, 1.12f, 0.95f));
                PlaceCustomMesh($"SunrayBeam_{i}_B", _sunrayMesh, _sunrayMat, parent, beamOrigin + new Vector3(0.12f, 0f, -0.12f), new Vector3(9f, 35f, -3f), new Vector3(0.6f, 1.12f, 0.6f));
            }

            var fillGo = new GameObject("SubterraneanAmbient");
            fillGo.transform.SetParent(parent, false);
            fillGo.transform.position = new Vector3(0f, FloorTopY + 6.5f, 4f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.28f, 0.36f, 0.42f);
            fill.intensity = 3.5f;
            fill.range = 48f;
            fill.shadows = LightShadows.None;
        }

        static void EnsureCustomAssets()
        {
            if (!AssetDatabase.IsValidFolder(Custom3DRoot))
            {
                Directory.CreateDirectory(Custom3DRoot);
                AssetDatabase.Refresh();
            }

            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            var spritesDefault = Shader.Find("Sprites/Default");
            _vineStemMat = EnsureMaterial($"{Custom3DRoot}/Vine_Stem.mat", urpLit, new Color(0.18f, 0.22f, 0.12f), 0.04f, doubleSided: true);
            _vineLeafMat = EnsureMaterial($"{Custom3DRoot}/Vine_Leaf.mat", urpLit, new Color(0.20f, 0.34f, 0.14f), 0.05f, doubleSided: true);
            _dirtMat = EnsureMaterial($"{Custom3DRoot}/Dirt_Soil.mat", urpLit, new Color(0.22f, 0.16f, 0.11f), 0.02f, doubleSided: false);

            _sunrayMat = AssetDatabase.LoadAssetAtPath<Material>($"{Custom3DRoot}/Sunray_Beam.mat");
            if (_sunrayMat == null)
            {
                _sunrayMat = new Material(spritesDefault != null ? spritesDefault : urpLit);
                AssetDatabase.CreateAsset(_sunrayMat, $"{Custom3DRoot}/Sunray_Beam.mat");
            }
            if (spritesDefault != null) _sunrayMat.shader = spritesDefault;
            _sunrayMat.color = new Color(0.82f, 0.90f, 0.78f, 1.0f);
            _sunrayMat.renderQueue = 3000;
            EditorUtility.SetDirty(_sunrayMat);

            _vineHangingMesh = EnsureMesh($"{Custom3DRoot}/Vine_Hanging.asset", BuildHangingVineMesh(), overwrite: true);
            _vineCreepMesh = EnsureMesh($"{Custom3DRoot}/Vine_Creep.asset", BuildCreepingVineMesh(), overwrite: true);
            _dirtMoundMesh = EnsureMesh($"{Custom3DRoot}/DirtMound.asset", BuildDirtMoundMesh(), overwrite: false);
            _sunrayMesh = EnsureMesh($"{Custom3DRoot}/SunrayShaft.asset", BuildSunrayShaftMesh(), overwrite: true);

            EnsureBrokenCeilingVariants();
            EnsureBrokenWallVariants();
        }

        static Material EnsureMaterial(string path, Shader shader, Color color, float smoothness, bool doubleSided)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            if (doubleSided) mat.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Mesh EnsureMesh(string path, Mesh generated, bool overwrite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                if (!overwrite)
                {
                    Object.DestroyImmediate(generated);
                    return existing;
                }
                existing.Clear();
                existing.indexFormat = generated.indexFormat;
                existing.SetVertices(generated.vertices);
                if (generated.normals != null && generated.normals.Length == generated.vertexCount)
                    existing.SetNormals(generated.normals);
                if (generated.uv != null && generated.uv.Length == generated.vertexCount)
                    existing.SetUVs(0, generated.uv);
                if (generated.colors != null && generated.colors.Length == generated.vertexCount)
                    existing.SetColors(generated.colors);
                existing.SetTriangles(generated.triangles, 0);
                existing.RecalculateBounds();
                Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }
            AssetDatabase.CreateAsset(generated, path);
            AssetDatabase.SaveAssets();
            return generated;
        }

        static Mesh BuildSunrayShaftMesh()
        {
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();

            int segs = 16;
            float rTop = 0.85f;
            float rBot = 2.5f;
            float h = 8.4f;

            for (int i = 0; i < segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * rTop, 0f, Mathf.Sin(a) * rTop));
                colors.Add(new Color(1f, 1f, 1f, 0.16f));
            }
            for (int i = 0; i < segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * rBot, -h, Mathf.Sin(a) * rBot));
                colors.Add(new Color(1f, 1f, 1f, 0.0f));
            }

            for (int i = 0; i < segs; i++)
            {
                int n = (i + 1) % segs;
                tris.Add(i); tris.Add(segs + n); tris.Add(n);
                tris.Add(i); tris.Add(segs + i); tris.Add(segs + n);
            }

            var m = new Mesh { name = "SunrayShaft" };
            m.SetVertices(verts);
            m.SetColors(colors);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static void EnsureBrokenCeilingVariants()
        {
            var srcPrefab = LoadLpd("01_Structure/SM_LPD_Struct_CeilGroinRibbed_01.prefab");
            var srcMf = srcPrefab != null ? srcPrefab.GetComponentInChildren<MeshFilter>() : null;
            var srcMr = srcPrefab != null ? srcPrefab.GetComponentInChildren<MeshRenderer>() : null;
            if (srcMf == null || srcMf.sharedMesh == null || srcMr == null) return;

            var srcMesh = srcMf.sharedMesh;
            var srcMat = srcMr.sharedMaterial;

            _ceilHoleCenterPrefab = BuildBrokenCeilingPrefab(
                "CeilGroinRibbed_HoleCenter", srcMesh, srcMat,
                c => IsInsideJaggedCircle(c.x, c.z, 0f, 0f, 0.95f, 0.14f),
                new[] { new Vector3(0f, 0f, 0.95f) });

            _ceilHoleMultiPrefab = BuildBrokenCeilingPrefab(
                "CeilGroinRibbed_HoleMulti", srcMesh, srcMat,
                c => IsInsideJaggedCircle(c.x, c.z, -0.85f, 0.80f, 0.50f, 0.08f) ||
                     IsInsideJaggedCircle(c.x, c.z, 0.80f, 0.75f, 0.46f, 0.08f) ||
                     IsInsideJaggedCircle(c.x, c.z, -0.78f, -0.85f, 0.48f, 0.08f) ||
                     IsInsideJaggedCircle(c.x, c.z, 0.85f, -0.76f, 0.50f, 0.08f),
                new[] {
                    new Vector3(-0.85f, 0.80f, 0.50f),
                    new Vector3(0.80f, 0.75f, 0.46f),
                    new Vector3(-0.78f, -0.85f, 0.48f),
                    new Vector3(0.85f, -0.76f, 0.50f)
                });

            _ceilHoleCornerPrefab = BuildBrokenCeilingPrefab(
                "CeilGroinRibbed_HoleCorner", srcMesh, srcMat,
                c => IsInsideJaggedCorner(c.x, c.z, 1f, 1f, 0.62f) ||
                     IsInsideJaggedCorner(c.x, c.z, -1f, -1f, 0.98f),
                new[] { new Vector3(1.25f, 1.25f, 0.68f), new Vector3(-1.35f, -1.35f, 0.50f) });
        }

        static void EnsureBrokenWallVariants()
        {
            var srcPrefab = LoadLpd("01_Structure/SM_LPD_Struct_WallCoursed_01.prefab");
            var srcMf = srcPrefab != null ? srcPrefab.GetComponentInChildren<MeshFilter>() : null;
            var srcMr = srcPrefab != null ? srcPrefab.GetComponentInChildren<MeshRenderer>() : null;
            if (srcMf == null || srcMf.sharedMesh == null || srcMr == null) return;

            var srcMesh = srcMf.sharedMesh;
            var srcMat = srcMr.sharedMaterial;

            _wallHoleCenterPrefab = BuildBrokenWallPrefab(
                "WallBroken_HoleCenter", srcMesh, srcMat,
                c => IsInsideJaggedCircle(c.x, c.y, 0f, 2.0f, 0.78f, 0.10f),
                tiltUpper: false);

            _wallTiltedFissurePrefab = BuildBrokenWallPrefab(
                "WallBroken_TiltedFissure", srcMesh, srcMat,
                c => Mathf.Abs(c.y - (1.55f - 0.30f * c.x)) < 0.28f || (c.y > 3.30f && c.x > 0.55f),
                tiltUpper: true);

            _wallDualBreachPrefab = BuildBrokenWallPrefab(
                "WallBroken_DualBreach", srcMesh, srcMat,
                c => IsInsideJaggedCircle(c.x, c.y, -0.82f, 2.45f, 0.52f, 0.08f) ||
                     IsInsideJaggedCircle(c.x, c.y, 0.86f, 1.45f, 0.48f, 0.08f),
                tiltUpper: false);

            _wallTopCornerPrefab = BuildBrokenWallPrefab(
                "WallBroken_TopCorner", srcMesh, srcMat,
                c => c.y > 2.25f + 0.55f * Mathf.Abs(c.x),
                tiltUpper: false);
        }

        static GameObject BuildBrokenWallPrefab(string name, Mesh srcMesh, Material mat, System.Func<Vector3, bool> shouldCut, bool tiltUpper)
        {
            var meshPath = $"{Custom3DRoot}/{name}.asset";
            var prefabPath = $"{Custom3DRoot}/{name}.prefab";

            var srcVerts = srcMesh.vertices;
            var srcNorms = srcMesh.normals;
            var srcUvs = srcMesh.uv;
            var srcTris = srcMesh.GetTriangles(0);

            // Group triangles by welded connected component so individual 3D bricks stay whole (no sliced spikes/shells).
            int n = srcVerts.Length;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            System.Func<int, int> find = x =>
            {
                while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                return x;
            };
            System.Action<int, int> union = (a, b) =>
            {
                a = find(a); b = find(b);
                if (a != b) parent[a] = b;
            };
            var posMap = new Dictionary<string, int>(n);
            for (int i = 0; i < n; i++)
            {
                Vector3 v = srcVerts[i];
                string k = Mathf.RoundToInt(v.x * 1000f) + "_" + Mathf.RoundToInt(v.y * 1000f) + "_" + Mathf.RoundToInt(v.z * 1000f);
                if (posMap.TryGetValue(k, out int first)) union(i, first);
                else posMap[k] = i;
            }
            for (int i = 0; i < srcTris.Length; i += 3)
            {
                union(srcTris[i], srcTris[i + 1]);
                union(srcTris[i + 1], srcTris[i + 2]);
            }

            var compTrisMap = new Dictionary<int, List<int>>();
            var compBoundsMap = new Dictionary<int, Bounds>();
            for (int i = 0; i < srcTris.Length; i += 3)
            {
                int r = find(srcTris[i]);
                if (!compTrisMap.TryGetValue(r, out var list))
                {
                    list = new List<int>();
                    compTrisMap[r] = list;
                    compBoundsMap[r] = new Bounds(srcVerts[srcTris[i]], Vector3.zero);
                }
                list.Add(srcTris[i]); list.Add(srcTris[i + 1]); list.Add(srcTris[i + 2]);
                Bounds b = compBoundsMap[r];
                b.Encapsulate(srcVerts[srcTris[i]]);
                b.Encapsulate(srcVerts[srcTris[i + 1]]);
                b.Encapsulate(srcVerts[srcTris[i + 2]]);
                compBoundsMap[r] = b;
            }

            var outVerts = new List<Vector3>();
            var outNorms = new List<Vector3>();
            var outUvs = new List<Vector2>();
            var outTris = new List<int>();
            var remap = new Dictionary<int, int>();

            Quaternion tiltRot = Quaternion.Euler(0f, 0f, -8f);
            Vector3 pivot = new Vector3(-1.55f, 1.95f, 0f);
            Vector2 coreUv = srcUvs.Length > 0 ? srcUvs[0] : new Vector2(0.84f, 0.97f);
            Vector2 corniceUv = coreUv;

            foreach (var kv in compTrisMap)
            {
                Bounds b = compBoundsMap[kv.Key];
                Vector3 c = b.center;
                List<int> cTris = kv.Value;

                bool isCore = b.size.y > 2.5f && b.size.z < 0.30f;
                if (isCore)
                {
                    if (cTris.Count > 0 && cTris[0] < srcUvs.Length) coreUv = srcUvs[cTris[0]];
                    continue;
                }

                bool isCornice = c.y > 3.70f && b.size.x > 3.5f;
                if (isCornice)
                {
                    if (cTris.Count > 0 && cTris[0] < srcUvs.Length) corniceUv = srcUvs[cTris[0]];
                    continue;
                }

                bool isPlinth = c.y < 0.35f && b.size.x > 3.5f;
                if (!isPlinth && shouldCut(c)) continue;

                bool doTilt = !isPlinth && tiltUpper && c.y > 1.55f - 0.30f * c.x;
                if (doTilt)
                {
                    Vector3 tc = pivot + tiltRot * (c - pivot);
                    if (Mathf.Abs(tc.x) > 1.94f || tc.y > 3.92f || tc.y < 0.55f) continue;
                }

                remap.Clear();
                for (int i = 0; i < cTris.Count; i++)
                {
                    int srcIdx = cTris[i];
                    if (!remap.TryGetValue(srcIdx, out int dstIdx))
                    {
                        dstIdx = outVerts.Count;
                        remap[srcIdx] = dstIdx;
                        Vector3 vp = srcVerts[srcIdx];
                        Vector3 np = srcNorms[srcIdx];
                        if (doTilt)
                        {
                            vp = pivot + tiltRot * (vp - pivot);
                            np = tiltRot * np;
                        }
                        outVerts.Add(vp);
                        outNorms.Add(np);
                        outUvs.Add(srcIdx < srcUvs.Length ? srcUvs[srcIdx] : coreUv);
                    }
                    outTris.Add(dstIdx);
                }
            }

            // Rebuild the inner mortar core and top cornice from solid closed 3D stone blocks so cut edges have zero open shells.
            for (float cx = -1.89f; cx <= 1.89f; cx += 0.21f)
            {
                for (float cy = 0.66f; cy <= 3.66f; cy += 0.20f)
                {
                    Vector3 c = new Vector3(cx, cy, 0f);
                    if (shouldCut(c)) continue;
                    Vector3 bSize = new Vector3(0.22f, 0.21f, 0.24f);
                    if (tiltUpper && cy > 1.55f - 0.30f * cx)
                    {
                        Vector3 tc = pivot + tiltRot * (c - pivot);
                        if (Mathf.Abs(tc.x) > 1.92f || tc.y > 3.86f || tc.y < 0.58f) continue;
                        AddStoneBox(outVerts, outNorms, outUvs, outTris, tc, tiltRot, bSize, coreUv);
                    }
                    else
                    {
                        AddStoneBox(outVerts, outNorms, outUvs, outTris, c, Quaternion.identity, bSize, coreUv);
                    }
                }
            }

            for (float cx = -1.875f; cx <= 1.875f; cx += 0.25f)
            {
                Vector3 c = new Vector3(cx, 3.86f, 0f);
                if (shouldCut(c)) continue;
                Vector3 bSize = new Vector3(0.26f, 0.26f, 0.38f);
                if (tiltUpper && c.y > 1.55f - 0.30f * cx)
                {
                    Vector3 tc = pivot + tiltRot * (c - pivot);
                    if (Mathf.Abs(tc.x) > 1.90f || tc.y > 3.92f) continue;
                    AddStoneBox(outVerts, outNorms, outUvs, outTris, tc, tiltRot, bSize, corniceUv);
                }
                else
                {
                    AddStoneBox(outVerts, outNorms, outUvs, outTris, c, Quaternion.identity, bSize, corniceUv);
                }
            }

            var carved = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            carved.SetVertices(outVerts);
            carved.SetNormals(outNorms);
            carved.SetUVs(0, outUvs);
            carved.SetTriangles(outTris, 0);
            carved.RecalculateBounds();

            var meshAsset = EnsureMesh(meshPath, carved, overwrite: true);

            var temp = new GameObject(name);
            var mf = temp.AddComponent<MeshFilter>();
            mf.sharedMesh = meshAsset;
            var mr = temp.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        static bool IsInsideJaggedCircle(float x, float z, float cx, float cz, float r, float wobble)
        {
            float dx = x - cx;
            float dz = z - cz;
            float a = Mathf.Atan2(dz, dx);
            float rw = r + wobble * (0.6f * Mathf.Sin(5f * a + cx) + 0.4f * Mathf.Cos(7f * a - cz));
            return dx * dx + dz * dz < rw * rw;
        }

        static bool IsInsideJaggedCorner(float x, float z, float sx, float sz, float thresh)
        {
            float px = x * sx;
            float pz = z * sz;
            float j = 0.14f * Mathf.Sin(x * 5f + z * 3f);
            return px > thresh + j && pz > thresh - j;
        }

        static GameObject BuildBrokenCeilingPrefab(string name, Mesh srcMesh, Material mat, System.Func<Vector3, bool> shouldCut, Vector3[] rimCircles)
        {
            var meshPath = $"{Custom3DRoot}/{name}.asset";
            var prefabPath = $"{Custom3DRoot}/{name}.prefab";

            var verts = new List<Vector3>(srcMesh.vertices);
            var norms = new List<Vector3>(srcMesh.normals);
            var uvs = new List<Vector2>(srcMesh.uv);
            var tris = new List<int>(srcMesh.GetTriangles(0));

            // Subdivide triangles 3x (bounded) so carved edges are crisp with zero long spikes or 16-bit index overflow
            for (int pass = 0; pass < 3; pass++)
            {
                if (verts.Count > 45000) break;
                var nextTris = new List<int>(tris.Count * 2);
                var edgeMid = new Dictionary<long, int>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
                    Vector3 v0 = verts[i0], v1 = verts[i1], v2 = verts[i2];
                    float maxEdgeSq = Mathf.Max((v1 - v0).sqrMagnitude, Mathf.Max((v2 - v1).sqrMagnitude, (v0 - v2).sqrMagnitude));
                    if (maxEdgeSq < 0.03f)
                    {
                        nextTris.Add(i0); nextTris.Add(i1); nextTris.Add(i2);
                        continue;
                    }
                    int m01 = GetMidpoint(i0, i1, verts, norms, uvs, edgeMid);
                    int m12 = GetMidpoint(i1, i2, verts, norms, uvs, edgeMid);
                    int m20 = GetMidpoint(i2, i0, verts, norms, uvs, edgeMid);
                    nextTris.Add(i0); nextTris.Add(m01); nextTris.Add(m20);
                    nextTris.Add(m01); nextTris.Add(i1); nextTris.Add(m12);
                    nextTris.Add(m20); nextTris.Add(m12); nextTris.Add(i2);
                    nextTris.Add(m01); nextTris.Add(m12); nextTris.Add(m20);
                }
                tris = nextTris;
            }

            var keptTris = new List<int>(tris.Count);
            for (int i = 0; i < tris.Count; i += 3)
            {
                int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];
                Vector3 centroid = (verts[i0] + verts[i1] + verts[i2]) / 3f;
                if (!shouldCut(centroid))
                {
                    keptTris.Add(i0); keptTris.Add(i1); keptTris.Add(i2);
                }
            }

            // Add chunky 3D stepped masonry blocks around each hole rim (matching WallRuined_01)
            Vector2 stoneUv = uvs.Count > 0 ? uvs[0] : new Vector2(0.84f, 0.97f);
            var rng = new System.Random(name.GetHashCode());
            foreach (var rc in rimCircles)
            {
                int count = Mathf.Max(8, Mathf.RoundToInt(rc.z * 16f));
                for (int k = 0; k < count; k++)
                {
                    float a = (k / (float)count) * Mathf.PI * 2f;
                    float px = rc.x + Mathf.Cos(a) * rc.z;
                    float pz = rc.y + Mathf.Sin(a) * rc.z;
                    if (Mathf.Abs(px) > 1.92f || Mathf.Abs(pz) > 1.92f) continue;
                    // Seat stepped masonry blocks right at the visible underside of the vault (like WallRuined_01)
                    float py = 1.02f - 0.18f * (px * px + pz * pz) * 0.25f;
                    Vector3 bSize = new Vector3(
                        0.28f + (float)rng.NextDouble() * 0.16f,
                        0.28f + (float)rng.NextDouble() * 0.14f,
                        0.24f + (float)rng.NextDouble() * 0.14f);
                    Quaternion bRot = Quaternion.Euler((float)rng.NextDouble() * 14f - 7f, a * Mathf.Rad2Deg, (float)rng.NextDouble() * 14f - 7f);
                    AddStoneBox(verts, norms, uvs, keptTris, new Vector3(px, py, pz), bRot, bSize, stoneUv);
                }
            }

            var carved = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            carved.SetVertices(verts);
            carved.SetNormals(norms);
            carved.SetUVs(0, uvs);
            carved.SetTriangles(keptTris, 0);
            carved.RecalculateBounds();

            var meshAsset = EnsureMesh(meshPath, carved, overwrite: true);

            var temp = new GameObject(name);
            var mf = temp.AddComponent<MeshFilter>();
            mf.sharedMesh = meshAsset;
            var mr = temp.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                AssetDatabase.DeleteAsset(prefabPath);
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, prefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        static int GetMidpoint(int a, int b, List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, Dictionary<long, int> cache)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int idx)) return idx;
            idx = verts.Count;
            verts.Add((verts[a] + verts[b]) * 0.5f);
            norms.Add((norms[a] + norms[b]).normalized);
            uvs.Add((uvs[a] + uvs[b]) * 0.5f);
            cache[key] = idx;
            return idx;
        }

        static void AddStoneBox(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 center, Quaternion rot, Vector3 size, Vector2 stoneUv)
        {
            Vector3 h = size * 0.5f;
            Vector3[] faceNormals = { Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            Vector3[,] faceCorners = {
                { new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z) },
                { new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z) },
                { new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, -h.y, h.z) },
                { new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, -h.y, -h.z) },
                { new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, -h.y, h.z) },
                { new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, -h.y, -h.z) }
            };
            for (int f = 0; f < 6; f++)
            {
                int idx = v.Count;
                Vector3 wn = rot * faceNormals[f];
                for (int c = 0; c < 4; c++)
                {
                    v.Add(center + rot * faceCorners[f, c]);
                    n.Add(wn);
                    uv.Add(stoneUv);
                }
                t.Add(idx); t.Add(idx + 1); t.Add(idx + 2);
                t.Add(idx); t.Add(idx + 2); t.Add(idx + 3);
            }
        }

        static Mesh BuildHangingVineMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var rng = new System.Random(77);

            for (int s = 0; s < 3; s++)
            {
                float ox = (s - 1) * 0.22f;
                float oz = (s % 2 == 0 ? 0.1f : -0.1f);
                float len = 2.8f + s * 0.6f;
                int segs = 10;
                for (int i = 0; i < segs; i++)
                {
                    float t0 = (float)i / segs;
                    float t1 = (float)(i + 1) / segs;
                    Vector3 p0 = new Vector3(ox + Mathf.Sin(t0 * 5f + s) * 0.08f, -t0 * len, oz + Mathf.Cos(t0 * 4f + s) * 0.08f);
                    Vector3 p1 = new Vector3(ox + Mathf.Sin(t1 * 5f + s) * 0.08f, -t1 * len, oz + Mathf.Cos(t1 * 4f + s) * 0.08f);
                    AddSegmentRibbon(verts, tris, p0, p1, 0.03f);

                    for (int l = 0; l < 2; l++)
                    {
                        float yaw = (float)rng.NextDouble() * 360f;
                        Vector3 lp = Vector3.Lerp(p0, p1, 0.5f);
                        AddFacetedLeaf(verts, tris, lp, Quaternion.Euler(25f, yaw, 0f), 0.18f);
                    }
                }
            }

            var m = new Mesh { name = "Vine_Hanging" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildCreepingVineMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var rng = new System.Random(88);

            for (int b = 0; b < 5; b++)
            {
                float startX = (b - 2) * 0.35f;
                Vector3 cur = new Vector3(startX, -1.2f, 0f);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 next = cur + new Vector3(Mathf.Sin(i * 0.9f + b) * 0.18f, 0.32f, 0.02f);
                    AddSegmentRibbon(verts, tris, cur, next, 0.035f);
                    AddFacetedLeaf(verts, tris, next, Quaternion.Euler(-15f, rng.Next(-50, 50), rng.Next(-35, 35)), 0.22f);
                    AddFacetedLeaf(verts, tris, Vector3.Lerp(cur, next, 0.5f), Quaternion.Euler(15f, rng.Next(-60, 60), 0f), 0.18f);
                    cur = next;
                }
            }

            var m = new Mesh { name = "Vine_Creep" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh BuildDirtMoundMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int rings = 3;
            int segs = 10;
            var rng = new System.Random(99);

            verts.Add(new Vector3(0f, 0.28f, 0f));
            for (int r = 1; r <= rings; r++)
            {
                float t = (float)r / rings;
                float rad = t * 1.15f;
                float y = (1f - t * t) * 0.28f;
                for (int s = 0; s < segs; s++)
                {
                    float a = (s / (float)segs) * Mathf.PI * 2f;
                    float jitter = 0.85f + (float)rng.NextDouble() * 0.3f;
                    verts.Add(new Vector3(Mathf.Cos(a) * rad * jitter, y, Mathf.Sin(a) * rad * jitter));
                }
            }

            for (int s = 0; s < segs; s++)
            {
                int next = (s + 1) % segs;
                tris.Add(0); tris.Add(1 + next); tris.Add(1 + s);
            }
            for (int r = 1; r < rings; r++)
            {
                int r0 = 1 + (r - 1) * segs;
                int r1 = 1 + r * segs;
                for (int s = 0; s < segs; s++)
                {
                    int sn = (s + 1) % segs;
                    tris.Add(r0 + s); tris.Add(r0 + sn); tris.Add(r1 + sn);
                    tris.Add(r0 + s); tris.Add(r1 + sn); tris.Add(r1 + s);
                }
            }

            var m = new Mesh { name = "DirtMound" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static void AddSegmentRibbon(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, float halfW)
        {
            int idx = v.Count;
            v.Add(a + new Vector3(-halfW, 0f, 0f));
            v.Add(a + new Vector3(halfW, 0f, 0f));
            v.Add(b + new Vector3(halfW, 0f, 0f));
            v.Add(b + new Vector3(-halfW, 0f, 0f));
            t.Add(idx); t.Add(idx + 1); t.Add(idx + 2);
            t.Add(idx); t.Add(idx + 2); t.Add(idx + 3);
        }

        static void AddFacetedLeaf(List<Vector3> v, List<int> t, Vector3 origin, Quaternion rot, float size)
        {
            int idx = v.Count;
            v.Add(origin);
            v.Add(origin + rot * new Vector3(-size * 0.45f, -size * 0.2f, size * 0.45f));
            v.Add(origin + rot * new Vector3(0f, -size * 0.35f, size));
            v.Add(origin + rot * new Vector3(size * 0.45f, -size * 0.2f, size * 0.45f));
            t.Add(idx); t.Add(idx + 1); t.Add(idx + 2);
            t.Add(idx); t.Add(idx + 2); t.Add(idx + 3);
        }

        static void PlaceCustomMesh(string name, Mesh mesh, Material mat, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.eulerAngles = euler;
            go.transform.localScale = scale;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }

        static GameObject LoadLpd(string relPath) => AssetDatabase.LoadAssetAtPath<GameObject>($"{LpdRoot}/{relPath}");
        static GameObject LoadMfp(string relPath) => AssetDatabase.LoadAssetAtPath<GameObject>($"{MfpRoot}/{relPath}");
        static GameObject LoadMtp(string relPath) => AssetDatabase.LoadAssetAtPath<GameObject>($"{MtpRoot}/{relPath}");
        static GameObject LoadDfk(string relPath) => AssetDatabase.LoadAssetAtPath<GameObject>($"{DfkRoot}/{relPath}");

        static GameObject Place(GameObject prefab, string name, Transform parent, Vector3 pos, Vector3 euler, Vector3? scale = null, bool stripLights = false)
        {
            if (prefab == null) return null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = name;
            inst.transform.SetParent(parent, false);
            inst.transform.position = pos;
            inst.transform.eulerAngles = euler;
            if (scale.HasValue) inst.transform.localScale = scale.Value;
            if (stripLights)
            {
                foreach (var l in inst.GetComponentsInChildren<Light>(true)) l.enabled = false;
                foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            }
            return inst;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }
    }
}
