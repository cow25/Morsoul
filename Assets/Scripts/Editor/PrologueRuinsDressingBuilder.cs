using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Editor
{
    /// <summary>
    /// Dresses Assets/Scenes/3D/PrologueRuins.unity as an overgrown, ruined,
    /// and deserted subterranean research facility with sealed walls, small
    /// ceiling sunlight shafts, procedural 3D vines, dirt mounds, vegetation,
    /// and zero active/open fires.
    /// </summary>
    public static class PrologueRuinsDressingBuilder
    {
        const string ScenePath = "Assets/Scenes/3D/PrologueRuins.unity";
        const string LpdRoot = "Assets/[Import] LowPolyDungeonFree/Prefabs";
        const string Custom3DRoot = "Assets/Art/Environments/Custom/Ruins3D";
        const float FloorTopY = 0.69f;
        const float FloorBaseY = 0.49f;

        static Mesh _vineHangingMesh;
        static Mesh _vineCreepMesh;
        static Mesh _dirtMoundMesh;
        static Material _vineStemMat;
        static Material _vineLeafMat;
        static Material _dirtMat;

        [MenuItem("Morsoul/Scenes/3D/Rebuild Prologue Ruins Dressing", priority = 30)]
        public static void RebuildMenu() => Rebuild();

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
            BuildSealedWalls(archRoot);
            BuildRuinedColonnadeAndCeiling(archRoot);
            BuildResearchFacilityProps(propsRoot);
            BuildOvergrowthAndVines(natureRoot);
            BuildSubterraneanSunlight(lightsRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (openedHere) AssetDatabase.SaveAssets();
            Debug.Log("PrologueRuinsDressingBuilder: rebuilt overgrown ruined research facility.");
        }

        static void BuildFloors(Transform parent)
        {
            var worn = Load("02_Floors/SM_LPD_Floor_PavingFlagstoneWorn_01.prefab");
            var cracked = Load("02_Floors/SM_LPD_Floor_PavingCracked_01.prefab");
            var dirt = Load("02_Floors/SM_LPD_Floor_PavingDirt_01.prefab");
            var cobble = Load("02_Floors/SM_LPD_Floor_PavingCobble_01.prefab");

            var rng = new System.Random(107);
            // Extended to gz=20 so floor extends cleanly under Sealed Stone Gate with zero side gaps.
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

        static void BuildSealedWalls(Transform parent)
        {
            var ashlar = Load("01_Structure/SM_LPD_Struct_WallAshlar_01.prefab");
            var coursed = Load("01_Structure/SM_LPD_Struct_WallCoursed_01.prefab");
            var cracked = Load("01_Structure/SM_LPD_Struct_WallCracked_01.prefab");
            var rubbleWall = Load("01_Structure/SM_LPD_Struct_WallRubble_01.prefab");
            var corner = Load("01_Structure/SM_LPD_Struct_CornerPostQuoined_01.prefab");

            var rng = new System.Random(203);
            for (int gz = -8; gz <= 20; gz += 4)
            {
                var wBot = rng.Next(100) < 55 ? cracked : (rng.Next(2) == 0 ? rubbleWall : ashlar);
                var eBot = rng.Next(100) < 55 ? cracked : (rng.Next(2) == 0 ? rubbleWall : ashlar);
                Place(wBot, $"WallW_Bot_{gz}", parent, new Vector3(-19.8f, FloorTopY, gz), new Vector3(0f, 90f, 0f));
                Place(coursed, $"WallW_Top_{gz}", parent, new Vector3(-19.8f, FloorTopY + 4f, gz), new Vector3(0f, 90f, 0f));
                Place(ashlar, $"WallW_Cap_{gz}", parent, new Vector3(-19.8f, FloorTopY + 8f, gz), new Vector3(0f, 90f, 0f));

                Place(eBot, $"WallE_Bot_{gz}", parent, new Vector3(19.8f, FloorTopY, gz), new Vector3(0f, -90f, 0f));
                Place(coursed, $"WallE_Top_{gz}", parent, new Vector3(19.8f, FloorTopY + 4f, gz), new Vector3(0f, -90f, 0f));
                Place(ashlar, $"WallE_Cap_{gz}", parent, new Vector3(19.8f, FloorTopY + 8f, gz), new Vector3(0f, -90f, 0f));
            }

            // Corner piers sealing NW, NE, SW, SE junctions
            Place(corner, "Corner_NW", parent, new Vector3(-19.8f, FloorTopY, 18.8f), Vector3.zero, new Vector3(1.5f, 3.0f, 1.5f));
            Place(corner, "Corner_NE", parent, new Vector3(19.8f, FloorTopY, 18.8f), Vector3.zero, new Vector3(1.5f, 3.0f, 1.5f));
            Place(corner, "Corner_SW", parent, new Vector3(-19.8f, FloorTopY, -10.0f), Vector3.zero, new Vector3(1.5f, 3.0f, 1.5f));
            Place(corner, "Corner_SE", parent, new Vector3(19.8f, FloorTopY, -10.0f), Vector3.zero, new Vector3(1.5f, 3.0f, 1.5f));

            for (int gx = -18; gx <= 18; gx += 4)
            {
                var bot = rng.Next(100) < 60 ? cracked : rubbleWall;
                Place(bot, $"WallS_Bot_{gx}", parent, new Vector3(gx, FloorTopY, -10.0f), Vector3.zero);
                Place(coursed, $"WallS_Top_{gx}", parent, new Vector3(gx, FloorTopY + 4f, -10.0f), Vector3.zero);
                Place(ashlar, $"WallS_Cap_{gx}", parent, new Vector3(gx, FloorTopY + 8f, -10.0f), Vector3.zero);
            }

            // Sealed North wall aligned at z=18.8 + overlapping corner piers to eliminate side gaps on Sealed Stone Gate.
            foreach (int gx in new[] { -18, -14, -10, 10, 14, 18 })
            {
                var bot = Mathf.Abs(gx) == 14 ? rubbleWall : cracked;
                Place(bot, $"WallN_Bot_{gx}", parent, new Vector3(gx, FloorTopY, 18.8f), new Vector3(0f, 180f, 0f));
                Place(coursed, $"WallN_Top_{gx}", parent, new Vector3(gx, FloorTopY + 4f, 18.8f), new Vector3(0f, 180f, 0f));
                Place(ashlar, $"WallN_Cap_{gx}", parent, new Vector3(gx, FloorTopY + 8f, 18.8f), new Vector3(0f, 180f, 0f));
            }

            // Seal behind central gate seam
            for (int gx = -6; gx <= 6; gx += 4)
            {
                Place(ashlar, $"GateSealBack_Bot_{gx}", parent, new Vector3(gx, FloorTopY, 19.7f), new Vector3(0f, 180f, 0f));
                Place(coursed, $"GateSealBack_Top_{gx}", parent, new Vector3(gx, FloorTopY + 4f, 19.7f), new Vector3(0f, 180f, 0f));
                Place(ashlar, $"GateSealBack_Cap_{gx}", parent, new Vector3(gx, FloorTopY + 8f, 19.7f), new Vector3(0f, 180f, 0f));
            }

            // Tight masonry piers overlapping both sides of Sealed Stone Gate L/R (x=±10.0)
            Place(corner, "GateFrame_L", parent, new Vector3(-10.0f, FloorTopY, 18.75f), Vector3.zero, new Vector3(1.5f, 2.5f, 1.5f));
            Place(corner, "GateFrame_R", parent, new Vector3(10.05f, FloorTopY, 18.75f), Vector3.zero, new Vector3(1.5f, 2.5f, 1.5f));
        }

        static void BuildRuinedColonnadeAndCeiling(Transform parent)
        {
            var pillarOrnate = Load("01_Structure/SM_LPD_Struct_PillarOrnate_01.prefab");
            var pillarSquare = Load("01_Structure/SM_LPD_Struct_PillarSquare_01.prefab");
            var pillarBroken = Load("01_Structure/SM_LPD_Struct_PillarBroken_01.prefab");
            var ceilVault = Load("01_Structure/SM_LPD_Struct_CeilVault_01.prefab");
            var ceilBeams = Load("01_Structure/SM_LPD_Struct_CeilTimberBraced_01.prefab");
            var ceilFlat = Load("01_Structure/SM_LPD_Struct_CeilFlat_01.prefab");
            var ceilGrate = Load("01_Structure/SM_LPD_Struct_CeilOpenGrating_01.prefab");
            var rubbleMound = Load("01_Structure/SM_LPD_Struct_RubbleHeapMound_01.prefab");
            var rubbleAgainst = Load("01_Structure/SM_LPD_Struct_RubbleHeapAgainst_01.prefab");
            var debris = Load("01_Structure/SM_LPD_Struct_DebrisScatter_01.prefab");

            // Partially collapsed colonnade
            Place(pillarOrnate, "Pillar_L_South_Bot", parent, new Vector3(-7.0f, FloorTopY, -4.0f), Vector3.zero, new Vector3(1.25f, 1.25f, 1.25f));
            Place(pillarSquare, "Pillar_L_South_Top", parent, new Vector3(-7.0f, FloorTopY + 5.0f, -4.0f), Vector3.zero, new Vector3(1.25f, 1.0f, 1.25f));

            Place(pillarBroken, "Pillar_L_Mid_Broken", parent, new Vector3(-7.0f, FloorTopY, 3.0f), new Vector3(0f, 25f, 0f), new Vector3(1.1f, 1.1f, 1.1f));
            Place(rubbleMound, "Rubble_L_Mid", parent, new Vector3(-6.4f, FloorTopY, 3.6f), new Vector3(0f, 40f, 0f));

            Place(pillarOrnate, "Pillar_L_North_Bot", parent, new Vector3(-7.0f, FloorTopY, 12.5f), Vector3.zero, new Vector3(1.25f, 1.25f, 1.25f));
            Place(pillarSquare, "Pillar_L_North_Top", parent, new Vector3(-7.0f, FloorTopY + 5.0f, 12.5f), Vector3.zero, new Vector3(1.25f, 1.0f, 1.25f));

            Place(pillarBroken, "Pillar_R_South_Broken", parent, new Vector3(7.0f, FloorTopY, -4.0f), new Vector3(0f, -110f, 0f), new Vector3(1.1f, 1.1f, 1.1f));
            Place(debris, "Debris_R_South", parent, new Vector3(6.2f, FloorTopY, -3.2f), new Vector3(0f, 15f, 0f));

            Place(pillarOrnate, "Pillar_R_Mid_Bot", parent, new Vector3(7.0f, FloorTopY, 4.0f), Vector3.zero, new Vector3(1.25f, 1.25f, 1.25f));
            Place(pillarSquare, "Pillar_R_Mid_Top", parent, new Vector3(7.0f, FloorTopY + 5.0f, 4.0f), Vector3.zero, new Vector3(1.25f, 1.0f, 1.25f));

            Place(pillarOrnate, "Pillar_R_North_Bot", parent, new Vector3(7.0f, FloorTopY, 14.0f), Vector3.zero, new Vector3(1.25f, 1.25f, 1.25f));
            Place(pillarSquare, "Pillar_R_North_Top", parent, new Vector3(7.0f, FloorTopY + 5.0f, 14.0f), Vector3.zero, new Vector3(1.25f, 1.0f, 1.25f));

            Place(rubbleAgainst, "Collapse_SW", parent, new Vector3(-18.2f, FloorTopY, -7.8f), new Vector3(0f, 90f, 0f), new Vector3(1.3f, 1.3f, 1.3f));
            Place(rubbleAgainst, "Collapse_NW", parent, new Vector3(-14.5f, FloorTopY, 16.8f), new Vector3(0f, 180f, 0f), new Vector3(1.3f, 1.3f, 1.3f));
            Place(rubbleMound, "Collapse_CenterSide", parent, new Vector3(-2.8f, FloorTopY, 9.5f), new Vector3(0f, 70f, 0f));
            Place(debris, "Debris_Center1", parent, new Vector3(1.2f, FloorTopY, 3.5f), new Vector3(0f, 22f, 0f));
            Place(debris, "Debris_Center2", parent, new Vector3(-2.5f, FloorTopY, 11.5f), new Vector3(0f, -65f, 0f));

            // Ceiling with 3 small grated skylight collapse holes (too small/high to enter, let sunlight shafts in)
            for (int gx = -18; gx <= 18; gx += 4)
            {
                for (int gz = -8; gz <= 16; gz += 4)
                {
                    bool isSunHole = (gx == -2 && gz == 4) || (gx == 14 && gz == 8) || (gx == 2 && gz == -4);
                    if (isSunHole)
                    {
                        Place(ceilGrate, $"CeilSunHole_{gx}_{gz}", parent, new Vector3(gx, FloorTopY + 8.0f, gz), Vector3.zero);
                    }
                    else
                    {
                        var c = Mathf.Abs(gx) <= 6 ? ceilVault : ceilBeams;
                        Place(c, $"Ceil_{gx}_{gz}", parent, new Vector3(gx, FloorTopY + 8.0f, gz), Vector3.zero);
                        Place(ceilFlat, $"CeilCap_{gx}_{gz}", parent, new Vector3(gx, FloorTopY + 9.4f, gz), Vector3.zero);
                    }
                }
            }
        }

        static void BuildResearchFacilityProps(Transform parent)
        {
            // West Expedition Study & Archive Bay (around Desk at (-18.05, 0.69, 6.63))
            Place(Load("06_Furniture/SM_LPD_Furn_ChairPlain_01.prefab"), "Research_ChairTipped", parent, new Vector3(-16.1f, FloorTopY + 0.15f, 6.1f), new Vector3(12f, -125f, 78f), new Vector3(1.3f, 1.3f, 1.3f));
            Place(Load("06_Furniture/SM_LPD_Furn_TableSmall_01.prefab"), "Research_SideTable", parent, new Vector3(-18.2f, FloorTopY, 2.2f), new Vector3(0f, 82f, 0f), new Vector3(1.2f, 1.2f, 1.2f));
            Place(Load("04_Lighting/SM_LPD_Light_OilLampBrass_01.prefab"), "Research_UnlitLamp1", parent, new Vector3(-18.1f, FloorTopY + 0.94f, 2.2f), Vector3.zero, stripLights: true);
            Place(Load("06_Furniture/SM_LPD_Furn_CupboardOpen_01.prefab"), "Research_CupboardW1", parent, new Vector3(-18.8f, FloorTopY, 11.8f), new Vector3(0f, 90f, 0f), new Vector3(1.2f, 1.2f, 1.2f));
            Place(Load("06_Furniture/SM_LPD_Furn_CupboardClosed_01.prefab"), "Research_CupboardW2", parent, new Vector3(-18.8f, FloorTopY, 14.0f), new Vector3(0f, 90f, 0f), new Vector3(1.2f, 1.2f, 1.2f));
            Place(Load("06_Furniture/SM_LPD_Furn_ShelfWall_01.prefab"), "Research_ShelfW", parent, new Vector3(-19.2f, FloorTopY + 2.2f, 6.6f), new Vector3(0f, 90f, 0f), new Vector3(1.2f, 1.2f, 1.2f));
            Place(Load("05_Containers/SM_LPD_Crate_CrateOpen_01.prefab"), "Research_CrateOpen", parent, new Vector3(-17.8f, FloorTopY, 9.8f), new Vector3(0f, 25f, 0f));

            // Central & West Research Workbenches (specimen / alchemy / artifact examination)
            Place(Load("06_Furniture/SM_LPD_Furn_TableLong_01.prefab"), "Research_BenchCW", parent, new Vector3(-3.8f, FloorTopY, 4.2f), new Vector3(0f, 18f, 0f), new Vector3(1.15f, 1.15f, 1.15f));
            Place(Load("04_Lighting/SM_LPD_Light_OilLampBrass_01.prefab"), "Research_UnlitLamp2", parent, new Vector3(-4.2f, FloorTopY + 0.90f, 4.1f), Vector3.zero, stripLights: true);
            Place(Load("05_Containers/SM_LPD_Crate_PotClay_01.prefab"), "Research_SpecimenPot1", parent, new Vector3(-3.2f, FloorTopY + 0.90f, 4.4f), Vector3.zero, new Vector3(0.7f, 0.7f, 0.7f));
            Place(Load("06_Furniture/SM_LPD_Furn_StoolSquare_01.prefab"), "Research_StoolTipped", parent, new Vector3(-3.4f, FloorTopY + 0.2f, 5.4f), new Vector3(85f, 40f, 0f));

            Place(Load("06_Furniture/SM_LPD_Furn_TableTrestle_01.prefab"), "Research_TrestleE", parent, new Vector3(11.5f, FloorTopY, -1.5f), new Vector3(0f, -20f, 0f), new Vector3(1.15f, 1.15f, 1.15f));
            Place(Load("06_Furniture/SM_LPD_Furn_ShelfPlank_01.prefab"), "Research_ShelfE", parent, new Vector3(18.6f, FloorTopY, 0.5f), new Vector3(0f, -90f, 0f), new Vector3(1.15f, 1.15f, 1.15f));
            Place(Load("05_Containers/SM_LPD_Crate_MeasureBushel_01.prefab"), "Research_Bushel", parent, new Vector3(11.2f, FloorTopY + 0.90f, -1.4f), Vector3.zero);
            Place(Load("06_Furniture/SM_LPD_Furn_BenchPlank_01.prefab"), "Research_BenchS", parent, new Vector3(-11.0f, FloorTopY, -2.5f), new Vector3(0f, 12f, 0f));

            // Botanical Specimen Hanging Jars (scaled 0.04f from 20m FBX -> ~0.8m)
            var jar1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HangingPlantsVolumeFive/Prefabs/BotanicalGardenJar01 (1).prefab");
            var jar2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HangingPlantsVolumeFive/Prefabs/BotanicalGardenJar01 (2).prefab");
            var jar3 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HangingPlantsVolumeFive/Prefabs/BotanicalGardenJar01 (3).prefab");
            var jar4 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/HangingPlantsVolumeFive/Prefabs/BotanicalGardenJar01 (4).prefab");
            Place(jar1, "Research_SpecimenJar_1", parent, new Vector3(-4.4f, FloorTopY + 4.4f, 4.2f), Vector3.zero, Vector3.one * 0.04f, stripLights: true);
            Place(jar2, "Research_SpecimenJar_2", parent, new Vector3(-16.8f, FloorTopY + 4.6f, 6.5f), new Vector3(0f, 45f, 0f), Vector3.one * 0.04f, stripLights: true);
            Place(jar3, "Research_SpecimenJar_3", parent, new Vector3(11.4f, FloorTopY + 4.5f, -1.2f), new Vector3(0f, -30f, 0f), Vector3.one * 0.04f, stripLights: true);
            Place(jar4, "Research_SpecimenJar_4", parent, new Vector3(13.2f, FloorTopY + 4.8f, 8.2f), new Vector3(0f, 80f, 0f), Vector3.one * 0.04f, stripLights: true);

            // All braziers & sconces unlit (zero active fires)
            var brazierUnlit = Load("04_Lighting/SM_LPD_Light_BrazierTripodUnlit_01.prefab");
            var sconceUnlit = Load("04_Lighting/SM_LPD_Light_SconceIronUnlit_01.prefab");
            Place(brazierUnlit, "Brazier_Unlit_0", parent, new Vector3(-4.5f, FloorTopY, -3.5f), Vector3.zero);
            Place(brazierUnlit, "Brazier_Tipped_1", parent, new Vector3(4.6f, FloorTopY + 0.1f, -3.2f), new Vector3(72f, 35f, 15f));
            Place(brazierUnlit, "Brazier_Unlit_2", parent, new Vector3(-6.2f, FloorTopY, 15.2f), Vector3.zero);
            Place(brazierUnlit, "Brazier_Unlit_3", parent, new Vector3(6.2f, FloorTopY, 15.2f), Vector3.zero);
            Place(sconceUnlit, "Sconce_Study_Unlit", parent, new Vector3(-19.55f, FloorTopY + 2.6f, 6.6f), new Vector3(0f, 90f, 0f));
            Place(sconceUnlit, "Sconce_EastKey_Unlit", parent, new Vector3(19.55f, FloorTopY + 2.6f, 7.6f), new Vector3(0f, -90f, 0f));

            // East collapsed debris & crates
            Place(Load("01_Structure/SM_LPD_Struct_RubbleHeapAgainst_01.prefab"), "East_RubbleWall", parent, new Vector3(18.0f, FloorTopY, 9.8f), new Vector3(0f, -90f, 0f));
            Place(Load("03_Doors/SM_LPD_Door_DoorBroken_01.prefab"), "East_BrokenDoor", parent, new Vector3(17.5f, FloorTopY, 13.8f), new Vector3(14f, -110f, 0f));
            Place(Load("05_Containers/SM_LPD_Crate_BarrelSide_01.prefab"), "East_BarrelSide", parent, new Vector3(17.8f, FloorTopY, 2.8f), new Vector3(0f, 30f, 0f));
            Place(Load("05_Containers/SM_LPD_Crate_HandcartEmpty_01.prefab"), "South_HandcartEmpty", parent, new Vector3(-11.5f, FloorTopY, -6.5f), new Vector3(0f, 55f, 0f));
            Place(Load("01_Structure/SM_LPD_Struct_ShoringProps_01.prefab"), "East_Shoring", parent, new Vector3(18.6f, FloorTopY, 14.0f), new Vector3(0f, -90f, 0f));
            Place(Load("01_Structure/SM_LPD_Struct_ShoringProps_01.prefab"), "West_Shoring", parent, new Vector3(-18.6f, FloorTopY, 14.2f), new Vector3(0f, 90f, 0f));
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

            // Dirt mounds beneath sunlight holes & rubble corners
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

            // Bushes sprouting from dirt mounds, cracked floor seams, & wall corners
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

            // Hanging vines from the 3 ceiling sunlight holes & colonnade beams
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

            // Creeping wall & pillar vines
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
            // Three natural daylight shafts pouring through small ceiling holes
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
                lgo.transform.position = new Vector3(t.x, FloorTopY + 8.5f, t.z);
                lgo.transform.LookAt(t + new Vector3(0.4f, 0f, 0.4f));
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = new Color(1.0f, 0.94f, 0.80f);
                l.intensity = 45f;
                l.range = 16f;
                l.spotAngle = 36f;
                l.innerSpotAngle = 14f;
                l.shadows = LightShadows.Soft;
            }

            // Very dim cool subterranean bounce so unlit corners remain atmospheric
            var fillGo = new GameObject("SubterraneanAmbient");
            fillGo.transform.SetParent(parent, false);
            fillGo.transform.position = new Vector3(0f, FloorTopY + 6.5f, 4f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.35f, 0.44f, 0.60f);
            fill.intensity = 4.0f;
            fill.range = 34f;
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
            _vineStemMat = EnsureMaterial($"{Custom3DRoot}/Vine_Stem.mat", urpLit, new Color(0.18f, 0.22f, 0.12f), 0.04f, doubleSided: true);
            _vineLeafMat = EnsureMaterial($"{Custom3DRoot}/Vine_Leaf.mat", urpLit, new Color(0.20f, 0.34f, 0.14f), 0.05f, doubleSided: true);
            _dirtMat = EnsureMaterial($"{Custom3DRoot}/Dirt_Soil.mat", urpLit, new Color(0.22f, 0.16f, 0.11f), 0.02f, doubleSided: false);

            _vineHangingMesh = EnsureMesh($"{Custom3DRoot}/Vine_Hanging.asset", BuildHangingVineMesh(), overwrite: true);
            _vineCreepMesh = EnsureMesh($"{Custom3DRoot}/Vine_Creep.asset", BuildCreepingVineMesh(), overwrite: true);
            _dirtMoundMesh = EnsureMesh($"{Custom3DRoot}/DirtMound.asset", BuildDirtMoundMesh(), overwrite: false);
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
                EditorUtility.CopySerialized(generated, existing);
                Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(generated, path);
            return generated;
        }

        static Mesh BuildHangingVineMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var rng = new System.Random(77);

            // 3 intertwined hanging strands
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

                    // Add 2 faceted leaves per segment
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

            // Branching wall creep tendrils
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

            verts.Add(new Vector3(0f, 0.28f, 0f)); // apex
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

            // Apex fan
            for (int s = 0; s < segs; s++)
            {
                int next = (s + 1) % segs;
                tris.Add(0);
                tris.Add(1 + next);
                tris.Add(1 + s);
            }
            // Outer rings
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

        static GameObject Load(string relPath) => AssetDatabase.LoadAssetAtPath<GameObject>($"{LpdRoot}/{relPath}");

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
