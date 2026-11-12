using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Ensures the newly imported asset packs (Fantasy Tileset, Village Interiors,
    /// Dungeon Objects, Retro Interior) use matching pixel-art import settings, PPU scale,
    /// bottom-center Y-sort pivots, and clean sprite slicing.
    /// </summary>
    public static class MarketPackAssetSetup
    {
        static readonly string[] FantasySingleDirs =
        {
            "Assets/Art/Environments/Fantasy_Tileset/Buildings",
            "Assets/Art/Environments/Fantasy_Tileset/Props",
            "Assets/Art/Environments/Fantasy_Tileset/Rocks",
            "Assets/Art/Environments/Fantasy_Tileset/Trees and Bushes",
        };

        const string VillageSpritesDir = "Assets/Art/Environments/Village_Interiors/furniture_and_props_sprites";
        const string SuppliesSheetPath = "Assets/Art/Environments/Dungeon_Objects/supplies_objects.png";
        const string OtherObjectsSheetPath = "Assets/Art/Environments/Dungeon_Objects/Other_objects.png";
        const string RetroSmallItemsPath = "Assets/Art/Environments/Retro_Interior/TopDownHouse_SmallItems.png";
        internal const string MaleIdleSheetPath = "Assets/Art/Environments/Fantasy_Tileset/Characters/Main Character/Character_Idle.png";
        internal const string MaleWalkSheetPath = "Assets/Art/Environments/Fantasy_Tileset/Characters/Main Character/Character_Walk.png";

        [MenuItem("Morsoul/Art/Setup Market Pack Sprites")]
        public static void EnsureAll()
        {
            foreach (var dir in FantasySingleDirs)
                ConfigureTopLevelSingleSprites(dir, ppu: 16f, tightCrop: true);

            ConfigureTopLevelSingleSprites(VillageSpritesDir, ppu: 28f, tightCrop: true);

            EnsureSlicedSheet(SuppliesSheetPath, "supplies_objects", ppu: 20f, useGrid16: false);
            EnsureSlicedSheet(OtherObjectsSheetPath, "Other_objects", ppu: 20f, useGrid16: false);
            EnsureSlicedSheet(RetroSmallItemsPath, "RetroItem", ppu: 18f, useGrid16: true);
            EnsureMainCharacterSheet(MaleIdleSheetPath, ppu: 18f);
            EnsureMainCharacterSheet(MaleWalkSheetPath, ppu: 18f);
            AssetDatabase.Refresh();
            PixelCrawlerPropAndAnimSetup.EnsureMaleVillagerController();
        }

        static void ConfigureTopLevelSingleSprites(string folder, float ppu, bool tightCrop)
        {
            if (!Directory.Exists(folder)) return;
            foreach (var file in Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
            {
                string assetPath = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                var desiredMode = tightCrop ? SpriteImportMode.Multiple : SpriteImportMode.Single;
                var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) ?? (reps.Length > 0 ? reps[0] as Sprite : null);
                string expectedName = Path.GetFileNameWithoutExtension(assetPath);

                bool hasPivotAtBottom = sp != null && sp.pivot.y <= 0.5f;
                bool hasCorrectReps = !tightCrop || (reps.Length == 1 && sp != null && sp.name == expectedName);

                bool dirty =
                    sp == null ||
                    importer.textureType != TextureImporterType.Sprite ||
                    importer.spriteImportMode != desiredMode ||
                    Mathf.Abs(importer.spritePixelsPerUnit - ppu) > 0.01f ||
                    importer.filterMode != FilterMode.Point ||
                    importer.textureCompression != TextureImporterCompression.Uncompressed ||
                    !hasPivotAtBottom ||
                    !hasCorrectReps;

                if (!dirty) continue;

                bool modeChanging = importer.spriteImportMode != desiredMode;
                var tis = new TextureImporterSettings();
                importer.ReadTextureSettings(tis);
                tis.textureType = TextureImporterType.Sprite;
                tis.spriteMode = (int)desiredMode;
                tis.spritePixelsPerUnit = ppu;
                tis.filterMode = FilterMode.Point;
                tis.mipmapEnabled = false;
                tis.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                tis.spritePivot = new Vector2(0.5f, 0f);
                importer.SetTextureSettings(tis);
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                EditorUtility.SetDirty(importer);

                if (modeChanging)
                    importer.SaveAndReimport();

                if (tightCrop)
                {
                    Rect crop = ComputeTightAlphaRect(assetPath);
                    var factory = new SpriteDataProviderFactories();
                    factory.Init();
                    var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
                    dataProvider.InitSpriteEditorDataProvider();
                    dataProvider.SetSpriteRects(new[]
                    {
                        new SpriteRect
                        {
                            name = expectedName,
                            spriteID = GUID.Generate(),
                            rect = crop,
                            alignment = SpriteAlignment.Custom,
                            pivot = new Vector2(0.5f, 0f),
                        }
                    });
                    dataProvider.Apply();
                    EditorUtility.SetDirty(importer);
                }

                importer.SaveAndReimport();
            }
        }

        static Rect ComputeTightAlphaRect(string assetPath)
        {
            var bytes = File.ReadAllBytes(assetPath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes);
            int w = tex.width;
            int h = tex.height;
            Color32[] pixels = tex.GetPixels32();
            UnityEngine.Object.DestroyImmediate(tex);

            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (pixels[row + x].a <= 8) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < minX || maxY < minY)
                return new Rect(0, 0, w, h);

            return new Rect(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
        }

        static void EnsureSlicedSheet(string assetPath, string prefix, float ppu, bool useGrid16)
        {
            if (!File.Exists(assetPath)) return;
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            if (importer.spriteImportMode == SpriteImportMode.Multiple &&
                Mathf.Abs(importer.spritePixelsPerUnit - ppu) < 0.01f &&
                AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath).Length >= 8)
                return;

            var bytes = File.ReadAllBytes(assetPath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes);
            int w = tex.width;
            int h = tex.height;
            Color32[] pixels = tex.GetPixels32();
            UnityEngine.Object.DestroyImmediate(tex);

            List<RectInt> boxes = useGrid16
                ? PixelCrawlerPropAndAnimSetup.ExtractGridAndRightTail(pixels, w, h, 16, 16, w, minExtent: 4)
                : PixelCrawlerPropAndAnimSetup.ExtractAlphaIslands(pixels, w, h, minExtent: 5, false, 0, 0);

            if (boxes.Count == 0) return;

            if (!useGrid16)
            {
                boxes.Sort((a, b) =>
                {
                    int bandA = (h - a.yMax) / 24;
                    int bandB = (h - b.yMax) / 24;
                    int cmp = bandA.CompareTo(bandB);
                    return cmp != 0 ? cmp : a.xMin.CompareTo(b.xMin);
                });
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu;
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
        }

        static void EnsureMainCharacterSheet(string assetPath, float ppu)
        {
            if (!File.Exists(assetPath)) return;
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            var existingReps = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            if (importer.spriteImportMode == SpriteImportMode.Multiple &&
                Mathf.Abs(importer.spritePixelsPerUnit - ppu) < 0.01f &&
                existingReps.Length == 16 &&
                existingReps[0] != null && existingReps[0].name.StartsWith("Char_", StringComparison.Ordinal))
                return;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(assetPath)))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                return;
            }

            int w = tex.width;
            int h = tex.height;
            Color32[] pixels = tex.GetPixels32();
            UnityEngine.Object.DestroyImmediate(tex);

            const int cellW = 40;
            const int cellH = 48;
            // Unity bottom-up rows in Fantasy Tileset Main Character sheets: Left (top row), Right, Up, Down (bottom row).
            string[] rowNames = { "Char_Down", "Char_Up", "Char_Right", "Char_Left" };
            var rectList = new List<SpriteRect>(16);

            for (int row = rowNames.Length - 1; row >= 0; row--)
            {
                int cy = row * cellH;
                if (cy + cellH > h) continue;
                for (int col = 0; col < 4; col++)
                {
                    int cx = col * cellW;
                    if (cx + cellW > w) continue;

                    int minX = cx + cellW, maxX = cx - 1, minY = cy + cellH, maxY = cy - 1;
                    for (int y = cy; y < cy + cellH; y++)
                    {
                        int rowOff = y * w;
                        for (int x = cx; x < cx + cellW; x++)
                        {
                            if (pixels[rowOff + x].a <= 10) continue;
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }

                    if (maxX < minX || maxY < minY) continue;
                    rectList.Add(new SpriteRect
                    {
                        name = $"{rowNames[row]}_{col}",
                        spriteID = GUID.Generate(),
                        rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
                        alignment = SpriteAlignment.BottomCenter,
                        pivot = new Vector2(0.5f, 0f),
                    });
                }
            }

            if (rectList.Count == 0) return;

            bool modeChanging = importer.spriteImportMode != SpriteImportMode.Multiple;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            EditorUtility.SetDirty(importer);
            if (modeChanging) importer.SaveAndReimport();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
            dataProvider.InitSpriteEditorDataProvider();
            dataProvider.SetSpriteRects(rectList.ToArray());
            dataProvider.Apply();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }
}
