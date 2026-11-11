using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Slices static prop/tree/building sprite sheets by connected alpha components
    /// (with bottom-center pivots for Y-sorting) and generates AnimatorControllers
    /// matching HubCharacterAnimator parameters for Pixel Crawler characters & NPCs.
    /// </summary>
    public static class PixelCrawlerPropAndAnimSetup
    {
        const string AnimRoot = "Assets/Animations";

        static readonly string[] PropSheets =
        {
            "Assets/Art/Environments/Props/Static/Rocks.png",
            "Assets/Art/Environments/Props/Static/Vegetation.png",
            "Assets/Art/Environments/Props/Static/Shadows.png",
            "Assets/Art/Environments/Props/Static/Resources.png",
            "Assets/Art/Environments/Props/Static/Furniture.png",
            "Assets/Art/Environments/Props/Static/Farm.png",
            "Assets/Art/Environments/Props/Static/Tools.png",
            "Assets/Art/Environments/Props/Static/Dungeon_Props.png",
            "Assets/Art/Environments/Props/Static/Esoteric.png",
            "Assets/Art/Environments/Props/Static/Meat.png",
            "Assets/Art/Environments/Props/Static/Pan.png",
            "Assets/Art/Environments/Buildings/Exterior/Props.png",
            "Assets/Art/Environments/Buildings/Exterior/Roofs.png",
            "Assets/Art/Environments/Buildings/Exterior/Shadows.png",
            "Assets/Art/Environments/Buildings/Interior/Interior_Props_01.png",
            "Assets/Art/Environments/Stations/Bonfire/Bonfire.png",
            "Assets/Art/Environments/Stations/Furnace/Furnace.png",
            "Assets/Art/Environments/Props/Trees/Model_01/Size_02.png",
            "Assets/Art/Environments/Props/Trees/Model_01/Size_03.png",
            "Assets/Art/Environments/Props/Trees/Model_01/Size_04.png",
            "Assets/Art/Environments/Props/Trees/Model_01/Size_05.png",
            "Assets/Art/Environments/Props/Trees/Model_02/Size_02.png",
            "Assets/Art/Environments/Props/Trees/Model_02/Size_03.png",
            "Assets/Art/Environments/Props/Trees/Model_02/Size_04.png",
            "Assets/Art/Environments/Props/Trees/Model_02/Size_05.png",
            "Assets/Art/Environments/Props/Trees/Model_03/Size_02.png",
            "Assets/Art/Environments/Props/Trees/Model_03/Size_03.png",
            "Assets/Art/Environments/Props/Trees/Model_03/Size_04.png",
            "Assets/Art/Environments/Props/Trees/Model_03/Size_05.png",
        };

        struct NpcAnimSpec
        {
            public string Name;
            public string IdleSheet;
            public string MoveSheet;
            public float IdleFps;
            public float MoveFps;
        }

        static readonly NpcAnimSpec[] NpcSpecs =
        {
            new NpcAnimSpec
            {
                Name = "NPC_CitizenF_PeasantA",
                IdleSheet = "Assets/Art/Characters/NPCs/Citizen_F/Peasant_A/Idle/Idle-Sheet.png",
                MoveSheet = "Assets/Art/Characters/NPCs/Citizen_F/Peasant_A/Walk/Walk-Sheet.png",
                IdleFps = 6f, MoveFps = 8f,
            },
            new NpcAnimSpec
            {
                Name = "NPC_CitizenF_TavernA",
                IdleSheet = "Assets/Art/Characters/NPCs/Citizen_F/Tavern_A/Idle/Idle_Side-Sheet.png",
                MoveSheet = "Assets/Art/Characters/NPCs/Citizen_F/Tavern_A/Walk/Walk_Side-Sheet.png",
                IdleFps = 6f, MoveFps = 8f,
            },
            new NpcAnimSpec
            {
                Name = "NPC_CitizenF_TavernB",
                IdleSheet = "Assets/Art/Characters/NPCs/Citizen_F/Tavern_B/Idle/Idle_Side-Sheet.png",
                MoveSheet = "Assets/Art/Characters/NPCs/Citizen_F/Tavern_B/Walk/Walk_Side-Sheet.png",
                IdleFps = 6f, MoveFps = 8f,
            },
            new NpcAnimSpec
            {
                Name = "NPC_Knight",
                IdleSheet = "Assets/Art/Characters/NPCs/Knight/Idle/Idle-Sheet.png",
                MoveSheet = "Assets/Art/Characters/NPCs/Knight/Run/Run-Sheet.png",
                IdleFps = 6f, MoveFps = 10f,
            },
            new NpcAnimSpec
            {
                Name = "NPC_Rogue",
                IdleSheet = "Assets/Art/Characters/NPCs/Rogue/Idle/Idle-Sheet.png",
                MoveSheet = "Assets/Art/Characters/NPCs/Rogue/Run/Run-Sheet.png",
                IdleFps = 6f, MoveFps = 10f,
            },
            new NpcAnimSpec
            {
                Name = "NPC_Wizard",
                IdleSheet = "Assets/Art/Characters/NPCs/Wizard/Idle/Idle-Sheet.png",
                MoveSheet = "Assets/Art/Characters/NPCs/Wizard/Run/Run-Sheet.png",
                IdleFps = 6f, MoveFps = 10f,
            },
        };

        [MenuItem("Morsoul/Art/Slice Props & Build Character Animators")]
        public static void SetupAllMenu() => SetupAll();

        public static (int sheetsSliced, int spritesCreated, int controllersBuilt) SetupAll()
        {
            int sheets = 0;
            int totalSprites = 0;
            foreach (var path in PropSheets)
            {
                if (!File.Exists(path)) continue;
                int count = SlicePropSheetByIslands(path);
                if (count > 0)
                {
                    sheets++;
                    totalSprites += count;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EnsureFolder($"{AnimRoot}/Player");
            EnsureFolder($"{AnimRoot}/NPCs");

            int controllers = 0;
            if (BuildPlayerBodyAController() != null) controllers++;
            foreach (var spec in NpcSpecs)
            {
                if (BuildNpcController(spec) != null) controllers++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"PixelCrawlerPropAndAnimSetup: sliced {sheets} prop sheets ({totalSprites} sprites) and built {controllers} AnimatorControllers.");
            return (sheets, totalSprites, controllers);
        }

        // --- Connected-Component Slicing ------------------------------------

        static int SlicePropSheetByIslands(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return 0;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(assetPath)))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                return 0;
            }

            int w = tex.width;
            int h = tex.height;
            Color32[] pixels = tex.GetPixels32();
            UnityEngine.Object.DestroyImmediate(tex);

            bool isRocks = assetPath.EndsWith("/Rocks.png", StringComparison.OrdinalIgnoreCase);
            bool isTreeM1S3 = assetPath.EndsWith("/Model_01/Size_03.png", StringComparison.OrdinalIgnoreCase);
            bool isBonfire = assetPath.EndsWith("/Bonfire/Bonfire.png", StringComparison.OrdinalIgnoreCase);
            var boxes = isTreeM1S3
                ? ExtractGridAndRightTail(pixels, w, h, cellW: 48, cellH: 96, tailStartX: 192, minExtent: 5)
                : isBonfire
                    ? ExtractGridAndRightTail(pixels, w, h, cellW: 32, cellH: 48, tailStartX: w, minExtent: 5)
                    : ExtractAlphaIslands(pixels, w, h, minExtent: 5,
                        skipTopLeftHeader: isRocks, headerWidth: 56, headerHeight: 14);
            if (boxes.Count == 0) return 0;

            if (assetPath.EndsWith("/Buildings/Exterior/Roofs.png", StringComparison.OrdinalIgnoreCase))
                SplitWideTopRoofs(boxes, h);

            // Sort top-to-bottom by rough 16px row band, then left-to-right.
            boxes.Sort((a, b) =>
            {
                int bandA = (h - a.yMax) / 16;
                int bandB = (h - b.yMax) / 16;
                int cmp = bandA.CompareTo(bandB);
                return cmp != 0 ? cmp : a.xMin.CompareTo(b.xMin);
            });

            string prefix = MakeSpritePrefix(assetPath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();

            var rects = new SpriteRect[boxes.Count];
            for (int i = 0; i < boxes.Count; i++)
            {
                var r = boxes[i];
                rects[i] = new SpriteRect
                {
                    name = $"{prefix}_{i:D2}",
                    spriteID = GUID.Generate(),
                    rect = new Rect(r.xMin, r.yMin, r.width, r.height),
                    alignment = SpriteAlignment.BottomCenter,
                    pivot = new Vector2(0.5f, 0f),
                };
            }

            dataProvider.SetSpriteRects(rects);
            dataProvider.Apply();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            return rects.Length;
        }

        static string MakeSpritePrefix(string assetPath)
        {
            string file = Path.GetFileNameWithoutExtension(assetPath);
            if (assetPath.Contains("/Trees/"))
            {
                string parent = Path.GetFileName(Path.GetDirectoryName(assetPath));
                return $"Tree_{parent}_{file}";
            }
            if (assetPath.Contains("/Buildings/Exterior/"))
                return $"Ext_{file}";
            if (assetPath.Contains("/Buildings/Interior/"))
                return $"Int_{file}";
            return file;
        }

        internal static List<RectInt> ExtractAlphaIslands(
            Color32[] pixels, int w, int h, int minExtent,
            bool skipTopLeftHeader, int headerWidth, int headerHeight)
        {
            var visited = new bool[w * h];
            var queue = new int[w * h];
            var results = new List<RectInt>();
            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

            for (int y = 0; y < h; y++)
            {
                int rowOff = y * w;
                for (int x = 0; x < w; x++)
                {
                    int idx = rowOff + x;
                    if (visited[idx] || pixels[idx].a <= 10) continue;

                    int head = 0, tail = 0;
                    queue[tail++] = idx;
                    visited[idx] = true;

                    int minX = x, maxX = x, minY = y, maxY = y;
                    while (head < tail)
                    {
                        int cur = queue[head++];
                        int cx = cur % w;
                        int cy = cur / w;
                        if (cx < minX) minX = cx;
                        if (cx > maxX) maxX = cx;
                        if (cy < minY) minY = cy;
                        if (cy > maxY) maxY = cy;

                        for (int d = 0; d < 8; d++)
                        {
                            int nx = cx + dx[d];
                            int ny = cy + dy[d];
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                            int nIdx = ny * w + nx;
                            if (!visited[nIdx] && pixels[nIdx].a > 10)
                            {
                                visited[nIdx] = true;
                                queue[tail++] = nIdx;
                            }
                        }
                    }

                    int bw = maxX - minX + 1;
                    int bh = maxY - minY + 1;
                    if (bw < minExtent && bh < minExtent) continue;
                    if (skipTopLeftHeader && minX < headerWidth && maxY >= h - headerHeight)
                        continue;

                    results.Add(new RectInt(minX, minY, bw, bh));
                }
            }

            return results;
        }

        static void SplitWideTopRoofs(List<RectInt> boxes, int texHeight)
        {
            for (int i = boxes.Count - 1; i >= 0; i--)
            {
                var r = boxes[i];
                if (r.width >= 240 && r.yMin >= texHeight - 110)
                {
                    int half = r.width / 2;
                    boxes.RemoveAt(i);
                    boxes.Insert(i, new RectInt(r.xMin + half, r.yMin, r.width - half, r.height));
                    boxes.Insert(i, new RectInt(r.xMin, r.yMin, half, r.height));
                }
            }
        }

        internal static List<RectInt> ExtractGridAndRightTail(
            Color32[] pixels, int w, int h, int cellW, int cellH, int tailStartX, int minExtent)
        {
            var results = new List<RectInt>();
            for (int cy = h - cellH; cy >= 0; cy -= cellH)
            {
                for (int cx = 0; cx + cellW <= tailStartX; cx += cellW)
                {
                    if (TryTightBounds(pixels, w, cx, cy, cellW, cellH, out var b))
                        results.Add(b);
                }
            }

            foreach (var r in ExtractAlphaIslands(pixels, w, h, minExtent, false, 0, 0))
            {
                if (r.xMin >= tailStartX) results.Add(r);
            }
            return results;
        }

        static bool TryTightBounds(Color32[] pixels, int w, int rx, int ry, int rw, int rh, out RectInt bounds)
        {
            int minX = rx + rw, maxX = rx - 1, minY = ry + rh, maxY = ry - 1;
            for (int y = ry; y < ry + rh; y++)
            {
                int rowOff = y * w;
                for (int x = rx; x < rx + rw; x++)
                {
                    if (pixels[rowOff + x].a > 10)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (maxX < minX)
            {
                bounds = default;
                return false;
            }
            bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        // --- Animation & AnimatorController Generation ------------------------

        static AnimatorController BuildPlayerBodyAController()
        {
            const string folder = AnimRoot + "/Player";
            const string controllerPath = folder + "/Player_BodyA.controller";

            var idleDown = CreateSpriteClip($"{folder}/BodyA_Idle_Down.anim",
                "Assets/Art/Characters/Player/Body_A/Idle_Base/Idle_Down-Sheet.png", 6f, true);
            var idleSide = CreateSpriteClip($"{folder}/BodyA_Idle_Side.anim",
                "Assets/Art/Characters/Player/Body_A/Idle_Base/Idle_Side-Sheet.png", 6f, true);
            var idleUp = CreateSpriteClip($"{folder}/BodyA_Idle_Up.anim",
                "Assets/Art/Characters/Player/Body_A/Idle_Base/Idle_Up-Sheet.png", 6f, true);

            var runDown = CreateSpriteClip($"{folder}/BodyA_Run_Down.anim",
                "Assets/Art/Characters/Player/Body_A/Run_Base/Run_Down-Sheet.png", 10f, true);
            var runSide = CreateSpriteClip($"{folder}/BodyA_Run_Side.anim",
                "Assets/Art/Characters/Player/Body_A/Run_Base/Run_Side-Sheet.png", 10f, true);
            var runUp = CreateSpriteClip($"{folder}/BodyA_Run_Up.anim",
                "Assets/Art/Characters/Player/Body_A/Run_Base/Run_Up-Sheet.png", 10f, true);

            if (idleDown == null || runDown == null) return null;

            var controller = ResetController(controllerPath);
            AddStandardParameters(controller);

            var sm = controller.layers[0].stateMachine;
            var idleTree = Create4DirBlendTree(controller, "Idle", idleDown, idleSide, idleUp);
            var runTree = Create4DirBlendTree(controller, "Run", runDown, runSide, runUp);

            var idleState = sm.AddState("Idle");
            idleState.motion = idleTree;
            sm.defaultState = idleState;

            var runState = sm.AddState("Run");
            runState.motion = runTree;

            WireMovingTransitions(idleState, runState);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorController BuildNpcController(NpcAnimSpec spec)
        {
            string folder = $"{AnimRoot}/NPCs";
            string controllerPath = $"{folder}/{spec.Name}.controller";

            var idleClip = CreateSpriteClip($"{folder}/{spec.Name}_Idle.anim", spec.IdleSheet, spec.IdleFps, true);
            var moveClip = CreateSpriteClip($"{folder}/{spec.Name}_Move.anim", spec.MoveSheet, spec.MoveFps, true);
            if (idleClip == null) return null;

            var controller = ResetController(controllerPath);
            AddStandardParameters(controller);

            var sm = controller.layers[0].stateMachine;
            var idleState = sm.AddState("Idle");
            idleState.motion = idleClip;
            sm.defaultState = idleState;

            var moveState = sm.AddState("Move");
            moveState.motion = moveClip != null ? moveClip : idleClip;

            WireMovingTransitions(idleState, moveState);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorController ResetController(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
                AssetDatabase.DeleteAsset(path);
            return AnimatorController.CreateAnimatorControllerAtPath(path);
        }

        static void AddStandardParameters(AnimatorController controller)
        {
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        }

        static BlendTree Create4DirBlendTree(
            AnimatorController controller, string name,
            AnimationClip down, AnimationClip side, AnimationClip up)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.SimpleDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveY",
                useAutomaticThresholds = false,
            };
            tree.AddChild(down, new Vector2(0f, -1f));
            tree.AddChild(side, new Vector2(1f, 0f));
            tree.AddChild(side, new Vector2(-1f, 0f));
            tree.AddChild(up, new Vector2(0f, 1f));
            AssetDatabase.AddObjectToAsset(tree, controller);
            return tree;
        }

        static void WireMovingTransitions(AnimatorState idle, AnimatorState move)
        {
            var toMove = idle.AddTransition(move);
            toMove.hasExitTime = false;
            toMove.duration = 0f;
            toMove.AddCondition(AnimatorConditionMode.If, 0f, "Moving");

            var toIdle = move.AddTransition(idle);
            toIdle.hasExitTime = false;
            toIdle.duration = 0f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
        }

        static AnimationClip CreateSpriteClip(string clipPath, string sheetPath, float fps, bool loop)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath)
                .OfType<Sprite>()
                .OrderBy(s => s.rect.x)
                .ToArray();
            if (sprites.Length == 0)
            {
                Debug.LogWarning($"PixelCrawlerPropAndAnimSetup: no sprites at '{sheetPath}'.");
                return null;
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip();
            clip.frameRate = fps;

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            var keys = new ObjectReferenceKeyframe[sprites.Length];
            float frameDuration = 1f / Mathf.Max(1f, fps);
            for (int i = 0; i < sprites.Length; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i * frameDuration,
                    value = sprites[i],
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            if (isNew) AssetDatabase.CreateAsset(clip, clipPath);
            else EditorUtility.SetDirty(clip);
            return clip;
        }

        static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string normalized = folderPath.Replace('\\', '/').TrimEnd('/');
            int slash = normalized.LastIndexOf('/');
            EnsureFolder(normalized.Substring(0, slash));
            AssetDatabase.CreateFolder(normalized.Substring(0, slash), normalized.Substring(slash + 1));
        }
    }
}
