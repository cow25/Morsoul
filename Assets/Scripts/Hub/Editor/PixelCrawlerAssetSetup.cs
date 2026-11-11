using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Automates sprite slicing, import settings, static Tile generation, and Tile Palettes
    /// for the organized Pixel Crawler hierarchy under Assets/Art and Assets/Tiles.
    /// </summary>
    public static class PixelCrawlerAssetSetup
    {
        const int TargetPpu = 16;
        const int TileSize = 16;
        const string StaticTilesRoot = "Assets/Tiles/StaticTiles";
        const string PalettesRoot = "Assets/Tiles/Palettes";

        static readonly string[] Grid16Tilesets =
        {
            "Assets/Art/Environments/Tilesets/Floors_Tiles.png",
            "Assets/Art/Environments/Tilesets/Wall_Tiles.png",
            "Assets/Art/Environments/Tilesets/Wall_Variations.png",
            "Assets/Art/Environments/Tilesets/Water_tiles.png",
            "Assets/Art/Environments/Tilesets/Dungeon_Tiles.png",
            "Assets/Art/Environments/Buildings/Exterior/Walls.png",
            "Assets/Art/Environments/Buildings/Interior/Interior_Walls_01.png",
        };

        [MenuItem("Morsoul/Art/Setup Pixel Crawler Assets (Slice Tilesets & Characters)")]
        public static void SetupAll()
        {
            HubArtFolderSetup.CreateFolders();

            int tilesetsSliced = 0;
            foreach (var path in Grid16Tilesets)
            {
                if (File.Exists(path) && SliceGrid(path, TileSize, TileSize, new Vector2(0.5f, 0.5f)))
                    tilesetsSliced++;
            }

            int sheetsSliced = SliceAllCharacterSheets("Assets/Art/Characters");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            int palettesBuilt = BuildTilePalettes();
            Debug.Log($"PixelCrawlerAssetSetup: sliced {tilesetsSliced} tilesets (16x16 @ 16 PPU), " +
                      $"{sheetsSliced} character/NPC sheets, and built {palettesBuilt} Tile Palettes.");
        }

        [MenuItem("Morsoul/Art/Build Tile Palettes from Sliced Tilesets")]
        public static void BuildTilePalettesMenu() => BuildTilePalettes();

        public static int BuildTilePalettes()
        {
            EnsureFolder(StaticTilesRoot);
            EnsureFolder(PalettesRoot);

            int built = 0;
            foreach (var texPath in Grid16Tilesets)
            {
                if (!File.Exists(texPath)) continue;
                string paletteName = texPath.Contains("/Buildings/Exterior/Walls.png")
                    ? "Exterior_Walls"
                    : Path.GetFileNameWithoutExtension(texPath);

                string tileFolder = $"{StaticTilesRoot}/{paletteName}";
                EnsureFolder(tileFolder);

                string palettePath = $"{PalettesRoot}/{paletteName}.prefab";
                if (!File.Exists(palettePath))
                {
                    var created = GridPaletteUtility.CreateNewPalette(
                        PalettesRoot,
                        paletteName,
                        GridLayout.CellLayout.Rectangle,
                        GridPalette.CellSizing.Automatic,
                        Vector3.one,
                        GridLayout.CellSwizzle.XYZ);
                    if (created == null) continue;
                }

                var subAssets = AssetDatabase.LoadAllAssetsAtPath(texPath);
                var coords = new List<(Vector3Int cell, Tile tile)>();
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var obj in subAssets)
                    {
                        if (!(obj is Sprite sprite)) continue;
                        if (!TryParseGridCoords(sprite.name, out int row, out int col)) continue;

                        string tileAssetPath = $"{tileFolder}/{sprite.name}.asset";
                        var tile = AssetDatabase.LoadAssetAtPath<Tile>(tileAssetPath);
                        if (tile == null)
                        {
                            tile = ScriptableObject.CreateInstance<Tile>();
                            tile.sprite = sprite;
                            tile.color = Color.white;
                            tile.colliderType = Tile.ColliderType.None;
                            AssetDatabase.CreateAsset(tile, tileAssetPath);
                        }
                        else if (tile.sprite != sprite)
                        {
                            tile.sprite = sprite;
                            EditorUtility.SetDirty(tile);
                        }

                        coords.Add((new Vector3Int(col, -row, 0), tile));
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                var instanceRoot = PrefabUtility.LoadPrefabContents(palettePath);
                try
                {
                    var tilemap = instanceRoot.GetComponentInChildren<Tilemap>();
                    if (tilemap != null)
                    {
                        tilemap.ClearAllTiles();
                        foreach (var (cell, tile) in coords)
                            tilemap.SetTile(cell, tile);
                    }
                    PrefabUtility.SaveAsPrefabAsset(instanceRoot, palettePath);
                    built++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(instanceRoot);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return built;
        }

        static bool TryParseGridCoords(string spriteName, out int row, out int col)
        {
            row = 0;
            col = 0;
            int last = spriteName.LastIndexOf('_');
            if (last <= 0) return false;
            int prev = spriteName.LastIndexOf('_', last - 1);
            if (prev < 0) return false;
            return int.TryParse(spriteName.Substring(prev + 1, last - prev - 1), out row)
                && int.TryParse(spriteName.Substring(last + 1), out col);
        }

        static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }

        static int SliceAllCharacterSheets(string rootFolder)
        {
            int count = 0;
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { rootFolder });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (!path.EndsWith("-Sheet.png", StringComparison.OrdinalIgnoreCase)) continue;

                var tex = ReadRawTexture(path);
                if (tex == null) continue;
                try
                {
                    int w = tex.width;
                    int h = tex.height;
                    int frameW = h;
                    int frameH = h;
                    Vector2 pivot = new Vector2(0.5f, 0f);

                    if (path.Contains("/Body_A/") || path.Contains("/Citizen_F/"))
                    {
                        // 64x64 padded frames with feet resting 16px from the bottom edge.
                        pivot = new Vector2(0.5f, 0.25f);
                    }
                    else if (path.Contains("/Death-Sheet.png") && w % 6 == 0)
                    {
                        // Knight (288x32 -> 48x32), Rogue/Wizard (384x32 -> 64x32)
                        frameW = w / 6;
                    }

                    if (frameW <= 0 || frameH <= 0 || w % frameW != 0) continue;
                    if (ApplySliceRects(path, BuildStripRects(Path.GetFileNameWithoutExtension(path), tex, frameW, frameH, pivot)))
                        count++;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            return count;
        }

        static bool SliceGrid(string assetPath, int cellW, int cellH, Vector2 pivot)
        {
            var tex = ReadRawTexture(assetPath);
            if (tex == null) return false;
            try
            {
                string prefix = Path.GetFileNameWithoutExtension(assetPath);
                var rects = new List<SpriteRect>();
                var pixels = tex.GetPixels32();
                int texW = tex.width;
                int cols = tex.width / cellW;
                int rows = tex.height / cellH;

                // Top-to-bottom, left-to-right naming matches Unity's Grid By Cell Size convention.
                for (int r = 0; r < rows; r++)
                {
                    int py = tex.height - (r + 1) * cellH;
                    for (int c = 0; c < cols; c++)
                    {
                        int px = c * cellW;
                        if (!HasVisiblePixels(pixels, texW, px, py, cellW, cellH)) continue;

                        rects.Add(new SpriteRect
                        {
                            name = $"{prefix}_{r}_{c}",
                            rect = new Rect(px, py, cellW, cellH),
                            alignment = SpriteAlignment.Custom,
                            pivot = pivot,
                            spriteID = GUID.Generate(),
                        });
                    }
                }
                return ApplySliceRects(assetPath, rects);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static List<SpriteRect> BuildStripRects(string prefix, Texture2D tex, int frameW, int frameH, Vector2 pivot)
        {
            var rects = new List<SpriteRect>();
            int frames = tex.width / frameW;
            for (int i = 0; i < frames; i++)
            {
                int px = i * frameW;
                rects.Add(new SpriteRect
                {
                    name = $"{prefix}_{i}",
                    rect = new Rect(px, 0, frameW, frameH),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivot,
                    spriteID = GUID.Generate(),
                });
            }
            return rects;
        }

        static bool ApplySliceRects(string assetPath, List<SpriteRect> rects)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return false;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = TargetPpu;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
            if (dataProvider == null) return false;

            dataProvider.InitSpriteEditorDataProvider();
            dataProvider.SetSpriteRects(rects.ToArray());
            dataProvider.Apply();

            importer.SaveAndReimport();
            return true;
        }

        static Texture2D ReadRawTexture(string assetPath)
        {
            if (!File.Exists(assetPath)) return null;
            byte[] bytes = File.ReadAllBytes(assetPath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                return null;
            }
            return tex;
        }

        static bool HasVisiblePixels(Color32[] pixels, int texW, int startX, int startY, int w, int h)
        {
            for (int y = startY; y < startY + h; y++)
            {
                int rowOffset = y * texW;
                for (int x = startX; x < startX + w; x++)
                {
                    if (pixels[rowOffset + x].a > 0) return true;
                }
            }
            return false;
        }
    }
}
