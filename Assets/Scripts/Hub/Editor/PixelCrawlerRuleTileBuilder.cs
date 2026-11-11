using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Builds RuleTile autotile assets and a combined Tile Palette from the sliced
    /// Pixel Crawler tilesets under Assets/Art/Environments/Tilesets.
    /// </summary>
    public static class PixelCrawlerRuleTileBuilder
    {
        const string FloorsTexPath = "Assets/Art/Environments/Tilesets/Floors_Tiles.png";
        const string WaterTexPath = "Assets/Art/Environments/Tilesets/Water_tiles.png";
        const string RuleTilesRoot = "Assets/Tiles/RuleTiles";
        const string PalettesRoot = "Assets/Tiles/Palettes";

        const int Any = 0;
        const int This = RuleTile.TilingRuleOutput.Neighbor.This;
        const int Not = RuleTile.TilingRuleOutput.Neighbor.NotThis;

        struct DonutSpec
        {
            public string FileName;
            public string TexName;
            public int ColOffset;
            public Tile.ColliderType Collider;
            public bool UseSecondaryBlockEdges;
        }

        struct IslandSpec
        {
            public string FileName;
            public int RowOffset;
            public int ColOffset;
            public int SolidStartRow;
            public int SolidEndRow;
        }

        static readonly DonutSpec[] FloorDonutSpecs =
        {
            new DonutSpec { FileName = "RT_Grass", TexName = "Floors_Tiles", ColOffset = 0, Collider = Tile.ColliderType.None, UseSecondaryBlockEdges = true },
            new DonutSpec { FileName = "RT_Cobble", TexName = "Floors_Tiles", ColOffset = 5, Collider = Tile.ColliderType.None, UseSecondaryBlockEdges = true },
            new DonutSpec { FileName = "RT_Dirt", TexName = "Floors_Tiles", ColOffset = 10, Collider = Tile.ColliderType.None, UseSecondaryBlockEdges = true },
        };

        static readonly IslandSpec[] FloorIslandSpecs =
        {
            new IslandSpec { FileName = "RT_Snow", RowOffset = 12, ColOffset = 0, SolidStartRow = 22, SolidEndRow = 25 },
            new IslandSpec { FileName = "RT_Sand", RowOffset = 12, ColOffset = 5, SolidStartRow = 22, SolidEndRow = 25 },
        };

        [MenuItem("Morsoul/Art/Build Pixel Crawler RuleTiles")]
        public static void BuildAllMenu() => BuildAll();

        public static int BuildAll()
        {
            EnsureFolder(RuleTilesRoot);
            EnsureFolder(PalettesRoot);

            var floorSprites = LoadSpriteMap(FloorsTexPath);
            var waterSprites = LoadSpriteMap(WaterTexPath);
            var builtTiles = new List<RuleTile>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var spec in FloorDonutSpecs)
                {
                    var rt = BuildDonutRuleTile(spec, floorSprites);
                    if (rt != null) builtTiles.Add(rt);
                }

                foreach (var spec in FloorIslandSpecs)
                {
                    var rt = BuildFloorIslandRuleTile(spec, floorSprites);
                    if (rt != null) builtTiles.Add(rt);
                }

                var water = BuildWaterShoreRuleTile(waterSprites);
                if (water != null) builtTiles.Add(water);

                var deepWater = BuildWaterDeepRuleTile(waterSprites);
                if (deepWater != null) builtTiles.Add(deepWater);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            BuildRuleTilesPalette(builtTiles);

            Debug.Log($"PixelCrawlerRuleTileBuilder: built {builtTiles.Count} RuleTiles in '{RuleTilesRoot}' and updated '{PalettesRoot}/RuleTiles_Palette.prefab'.");
            return builtTiles.Count;
        }

        static RuleTile BuildDonutRuleTile(DonutSpec spec, Dictionary<string, Sprite> sprites)
        {
            int c = spec.ColOffset;
            string prefix = spec.TexName;

            // Rows 10..11 hold clean interior fill tiles without Row 9's upper donut rim.
            var centerPool = CollectBlock(sprites, prefix, 10, 11, c + 1, c + 3);
            var defaultSprite = Get(sprites, prefix, 10, c + 2) ?? (centerPool.Count > 0 ? centerPool[0] : null);
            if (defaultSprite == null)
            {
                Debug.LogWarning($"PixelCrawlerRuleTileBuilder: missing sprites for {spec.FileName}");
                return null;
            }

            var rt = CreateOrLoadRuleTile($"{RuleTilesRoot}/{spec.FileName}.asset", defaultSprite, spec.Collider);
            rt.m_TilingRules.Clear();

            // 1. Interior fill (all 8 neighbors = This) with randomized variation pool.
            AddRandomRule(rt, centerPool.ToArray(), spec.Collider,
                nw: This, n: This, ne: This, w: This, e: This, sw: This, s: This, se: This);

            // 2. Inner corners (4 cardinals = This, 1 diagonal = NotThis).
            // Secondary block rows 4..5 avoid top-row corner clipping on row 0.
            int topInnerRow = spec.UseSecondaryBlockEdges ? 5 : 0;
            AddSingleRule(rt, Get(sprites, prefix, topInnerRow, c + 1), spec.Collider,
                n: This, w: This, e: This, s: This, se: Not);
            AddSingleRule(rt, Get(sprites, prefix, topInnerRow, c + 3), spec.Collider,
                n: This, w: This, e: This, s: This, sw: Not);
            AddSingleRule(rt, Get(sprites, prefix, 4, c + 1), spec.Collider,
                n: This, w: This, e: This, s: This, ne: Not);
            AddSingleRule(rt, Get(sprites, prefix, 4, c + 3), spec.Collider,
                n: This, w: This, e: This, s: This, nw: Not);

            // 3. Straight edges (3 cardinals = This, 1 opposite cardinal = NotThis).
            var southEdge = CollectExisting(sprites, prefix, (0, c + 2), (5, c + 2));
            AddRandomRule(rt, southEdge, spec.Collider, n: This, w: This, e: This, s: Not);

            AddSingleRule(rt, Get(sprites, prefix, 4, c + 2), spec.Collider, s: This, w: This, e: This, n: Not);

            var eastEdge = CollectExisting(sprites, prefix, (2, c + 0), (7, c + 0));
            AddRandomRule(rt, eastEdge, spec.Collider, n: This, s: This, w: This, e: Not);

            var westEdge = CollectExisting(sprites, prefix, (2, c + 4), (7, c + 4));
            AddRandomRule(rt, westEdge, spec.Collider, n: This, s: This, e: This, w: Not);

            // 4. Outer convex corners (2 adjacent cardinals = This, 2 opposite = NotThis).
            AddSingleRule(rt, Get(sprites, prefix, 1, c + 1), spec.Collider, n: This, w: This, s: Not, e: Not);
            AddSingleRule(rt, Get(sprites, prefix, 1, c + 3), spec.Collider, n: This, e: This, s: Not, w: Not);
            AddSingleRule(rt, Get(sprites, prefix, 3, c + 1), spec.Collider, s: This, w: This, n: Not, e: Not);
            AddSingleRule(rt, Get(sprites, prefix, 3, c + 3), spec.Collider, s: This, e: This, n: Not, w: Not);

            FinalizeRuleTile(rt);
            return rt;
        }

        static RuleTile BuildFloorIslandRuleTile(IslandSpec spec, Dictionary<string, Sprite> sprites)
        {
            const string prefix = "Floors_Tiles";
            int r = spec.RowOffset;
            int c = spec.ColOffset;

            var centerPool = CollectBlock(sprites, prefix, spec.SolidStartRow, spec.SolidEndRow, c, c + 4);
            var defaultSprite = Get(sprites, prefix, r + 2, c + 2) ?? (centerPool.Count > 0 ? centerPool[0] : null);
            if (defaultSprite == null)
                return null;

            var rt = CreateOrLoadRuleTile($"{RuleTilesRoot}/{spec.FileName}.asset", defaultSprite, Tile.ColliderType.None);
            rt.m_TilingRules.Clear();

            AddRandomRule(rt, centerPool.ToArray(), Tile.ColliderType.None,
                nw: This, n: This, ne: This, w: This, e: This, sw: This, s: This, se: This);

            // Straight edges of the convex island (rows 12..16).
            AddSingleRule(rt, Get(sprites, prefix, r, c + 2), Tile.ColliderType.None, s: This, w: This, e: This, n: Not);
            AddSingleRule(rt, Get(sprites, prefix, r + 4, c + 2), Tile.ColliderType.None, n: This, w: This, e: This, s: Not);
            AddSingleRule(rt, Get(sprites, prefix, r + 2, c), Tile.ColliderType.None, n: This, s: This, e: This, w: Not);
            AddSingleRule(rt, Get(sprites, prefix, r + 2, c + 4), Tile.ColliderType.None, n: This, s: This, w: This, e: Not);

            // Outer convex corners.
            AddSingleRule(rt, Get(sprites, prefix, r + 1, c + 1), Tile.ColliderType.None, s: This, e: This, n: Not, w: Not);
            AddSingleRule(rt, Get(sprites, prefix, r + 1, c + 3), Tile.ColliderType.None, s: This, w: This, n: Not, e: Not);
            AddSingleRule(rt, Get(sprites, prefix, r + 3, c + 1), Tile.ColliderType.None, n: This, e: This, s: Not, w: Not);
            AddSingleRule(rt, Get(sprites, prefix, r + 3, c + 3), Tile.ColliderType.None, n: This, w: This, s: Not, e: Not);

            // Single-tile caps match the convex petals around the island perimeter.
            AddSingleRule(rt, Get(sprites, prefix, r, c + 2), Tile.ColliderType.None, s: This, n: Not, w: Not, e: Not);
            AddSingleRule(rt, Get(sprites, prefix, r + 4, c + 2), Tile.ColliderType.None, n: This, s: Not, w: Not, e: Not);
            AddSingleRule(rt, Get(sprites, prefix, r + 2, c), Tile.ColliderType.None, e: This, w: Not, n: Not, s: Not);
            AddSingleRule(rt, Get(sprites, prefix, r + 2, c + 4), Tile.ColliderType.None, w: This, e: Not, n: Not, s: Not);

            FinalizeRuleTile(rt);
            return rt;
        }

        static RuleTile BuildWaterShoreRuleTile(Dictionary<string, Sprite> sprites)
        {
            const string prefix = "Water_tiles";
            const Tile.ColliderType col = Tile.ColliderType.Grid;

            var centerPool = CollectExisting(sprites, prefix,
                (1, 5), (2, 5), (3, 5), (4, 5), (2, 2));
            // Note: (2,2) in Water_tiles is grass center of island; keep pure water ripples only!
            var pureWaterCenter = CollectExisting(sprites, prefix,
                (0, 4), (0, 5), (1, 5), (2, 5), (3, 5), (4, 4), (4, 5), (11, 1), (11, 2), (11, 3));
            var defaultSprite = Get(sprites, prefix, 2, 5) ?? (pureWaterCenter.Length > 0 ? pureWaterCenter[0] : null);
            if (defaultSprite == null)
                return null;

            var rt = CreateOrLoadRuleTile($"{RuleTilesRoot}/RT_Water.asset", defaultSprite, col);
            rt.m_TilingRules.Clear();

            // 1. Interior water (all 8 = This): mix pure water tiles (calm + light ripples).
            var calmWater = CollectExisting(sprites, prefix, (1, 5), (2, 5), (3, 5), (4, 5), (1, 6), (2, 6), (3, 6));
            if (calmWater.Length == 0) calmWater = new[] { defaultSprite };
            AddRandomRule(rt, calmWater, col,
                nw: This, n: This, ne: This, w: This, e: This, sw: This, s: This, se: This);

            // 2. Inner corners around land jutting into water (4 cardinals = This, 1 diag = NotThis).
            AddSingleRule(rt, Get(sprites, prefix, 0, 1), col, n: This, w: This, e: This, s: This, se: Not);
            AddSingleRule(rt, Get(sprites, prefix, 0, 3), col, n: This, w: This, e: This, s: This, sw: Not);
            AddSingleRule(rt, Get(sprites, prefix, 4, 1), col, n: This, w: This, e: This, s: This, ne: Not);
            AddSingleRule(rt, Get(sprites, prefix, 4, 3), col, n: This, w: This, e: This, s: This, nw: Not);

            // 3. Straight shoreline edges (water on 3 sides, land on 1 side).
            // Note: secondary water island block starts at col 6 (center col 8), not col 5.
            // (0,2): water N, shoreline/grass S => S = Not.
            AddRandomRule(rt, CollectExisting(sprites, prefix, (0, 2), (0, 8)), col, n: This, w: This, e: This, s: Not);
            // (4,2): water S, shoreline/grass N => N = Not.
            AddRandomRule(rt, CollectExisting(sprites, prefix, (4, 2), (4, 8)), col, s: This, w: This, e: This, n: Not);
            // (2,0): water W, shoreline/grass E => E = Not.
            AddSingleRule(rt, Get(sprites, prefix, 2, 0), col, n: This, s: This, w: This, e: Not);
            // (2,4): water E, shoreline/grass W => W = Not.
            AddSingleRule(rt, Get(sprites, prefix, 2, 4), col, n: This, s: This, e: This, w: Not);

            // 4. Outer convex water corners (water only on 2 adjacent cardinals).
            AddSingleRule(rt, Get(sprites, prefix, 1, 1), col, n: This, w: This, s: Not, e: Not);
            AddSingleRule(rt, Get(sprites, prefix, 1, 3), col, n: This, e: This, s: Not, w: Not);
            AddSingleRule(rt, Get(sprites, prefix, 3, 1), col, s: This, w: This, n: Not, e: Not);
            AddSingleRule(rt, Get(sprites, prefix, 3, 3), col, s: This, e: This, n: Not, w: Not);

            FinalizeRuleTile(rt);
            return rt;
        }

        static RuleTile BuildWaterDeepRuleTile(Dictionary<string, Sprite> sprites)
        {
            const string prefix = "Water_tiles";
            const Tile.ColliderType col = Tile.ColliderType.Grid;

            var centerPool = CollectBlock(sprites, prefix, 11, 13, 1, 3);
            var defaultSprite = Get(sprites, prefix, 12, 2) ?? (centerPool.Count > 0 ? centerPool[0] : null);
            if (defaultSprite == null)
                return null;

            var rt = CreateOrLoadRuleTile($"{RuleTilesRoot}/RT_WaterDeep.asset", defaultSprite, col);
            rt.m_TilingRules.Clear();

            AddRandomRule(rt, centerPool.ToArray(), col,
                nw: This, n: This, ne: This, w: This, e: This, sw: This, s: This, se: This);

            AddSingleRule(rt, Get(sprites, prefix, 10, 2), col, s: This, w: This, e: This, n: Not);
            AddSingleRule(rt, Get(sprites, prefix, 14, 2), col, n: This, w: This, e: This, s: Not);
            AddSingleRule(rt, Get(sprites, prefix, 12, 0), col, n: This, s: This, e: This, w: Not);
            AddSingleRule(rt, Get(sprites, prefix, 12, 4), col, n: This, s: This, w: This, e: Not);

            AddSingleRule(rt, Get(sprites, prefix, 11, 1), col, s: This, e: This, n: Not, w: Not);
            AddSingleRule(rt, Get(sprites, prefix, 11, 3), col, s: This, w: This, n: Not, e: Not);
            AddSingleRule(rt, Get(sprites, prefix, 13, 1), col, n: This, e: This, s: Not, w: Not);
            AddSingleRule(rt, Get(sprites, prefix, 13, 3), col, n: This, w: This, s: Not, e: Not);

            FinalizeRuleTile(rt);
            return rt;
        }

        static RuleTile CreateOrLoadRuleTile(string assetPath, Sprite defaultSprite, Tile.ColliderType defaultCollider)
        {
            var rt = AssetDatabase.LoadAssetAtPath<RuleTile>(assetPath);
            if (rt == null)
            {
                rt = ScriptableObject.CreateInstance<RuleTile>();
                AssetDatabase.CreateAsset(rt, assetPath);
            }

            rt.m_DefaultSprite = defaultSprite;
            rt.m_DefaultColliderType = defaultCollider;
            return rt;
        }

        static void FinalizeRuleTile(RuleTile rt)
        {
            rt.UpdateNeighborPositions();
            EditorUtility.SetDirty(rt);
        }

        static void AddSingleRule(
            RuleTile rt,
            Sprite sprite,
            Tile.ColliderType collider,
            int nw = Any, int n = Any, int ne = Any,
            int w = Any, int e = Any,
            int sw = Any, int s = Any, int se = Any)
        {
            if (sprite == null) return;
            var rule = BuildRule(RuleTile.TilingRuleOutput.OutputSprite.Single, new[] { sprite }, collider, nw, n, ne, w, e, sw, s, se);
            rt.m_TilingRules.Add(rule);
        }

        static void AddRandomRule(
            RuleTile rt,
            Sprite[] sprites,
            Tile.ColliderType collider,
            int nw = Any, int n = Any, int ne = Any,
            int w = Any, int e = Any,
            int sw = Any, int s = Any, int se = Any)
        {
            if (sprites == null || sprites.Length == 0) return;
            var output = sprites.Length == 1
                ? RuleTile.TilingRuleOutput.OutputSprite.Single
                : RuleTile.TilingRuleOutput.OutputSprite.Random;
            var rule = BuildRule(output, sprites, collider, nw, n, ne, w, e, sw, s, se);
            rule.m_PerlinScale = 0.5f;
            rt.m_TilingRules.Add(rule);
        }

        static RuleTile.TilingRule BuildRule(
            RuleTile.TilingRuleOutput.OutputSprite output,
            Sprite[] sprites,
            Tile.ColliderType collider,
            int nw, int n, int ne, int w, int e, int sw, int s, int se)
        {
            var rule = new RuleTile.TilingRule
            {
                m_Output = output,
                m_Sprites = sprites,
                m_ColliderType = collider,
                m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                m_NeighborPositions = new List<Vector3Int>
                {
                    new Vector3Int(-1, 1, 0),  // 0 NW
                    new Vector3Int(0, 1, 0),   // 1 N
                    new Vector3Int(1, 1, 0),   // 2 NE
                    new Vector3Int(-1, 0, 0),  // 3 W
                    new Vector3Int(1, 0, 0),   // 4 E
                    new Vector3Int(-1, -1, 0), // 5 SW
                    new Vector3Int(0, -1, 0),  // 6 S
                    new Vector3Int(1, -1, 0),  // 7 SE
                },
                m_Neighbors = new List<int> { nw, n, ne, w, e, sw, s, se },
            };
            return rule;
        }

        static void BuildRuleTilesPalette(List<RuleTile> tiles)
        {
            const string paletteName = "RuleTiles_Palette";
            string palettePath = $"{PalettesRoot}/{paletteName}.prefab";
            if (!File.Exists(palettePath))
            {
                var created = GridPaletteUtility.CreateNewPalette(
                    PalettesRoot, paletteName, GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
                if (created == null)
                    return;
            }

            var instanceRoot = PrefabUtility.LoadPrefabContents(palettePath);
            try
            {
                var tilemap = instanceRoot.GetComponentInChildren<Tilemap>();
                if (tilemap != null)
                {
                    tilemap.ClearAllTiles();
                    for (int i = 0; i < tiles.Count; i++)
                        tilemap.SetTile(new Vector3Int(i, 0, 0), tiles[i]);
                }
                PrefabUtility.SaveAsPrefabAsset(instanceRoot, palettePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instanceRoot);
            }
            AssetDatabase.SaveAssets();
        }

        static Dictionary<string, Sprite> LoadSpriteMap(string texturePath)
        {
            var map = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(texturePath))
            {
                if (obj is Sprite sprite)
                    map[sprite.name] = sprite;
            }
            return map;
        }

        static Sprite Get(Dictionary<string, Sprite> map, string prefix, int row, int col)
        {
            map.TryGetValue($"{prefix}_{row}_{col}", out var sprite);
            return sprite;
        }

        static List<Sprite> CollectBlock(Dictionary<string, Sprite> map, string prefix, int rStart, int rEnd, int cStart, int cEnd)
        {
            var list = new List<Sprite>();
            for (int r = rStart; r <= rEnd; r++)
            {
                for (int c = cStart; c <= cEnd; c++)
                {
                    var s = Get(map, prefix, r, c);
                    if (s != null) list.Add(s);
                }
            }
            return list;
        }

        static Sprite[] CollectExisting(Dictionary<string, Sprite> map, string prefix, params (int row, int col)[] coords)
        {
            var list = new List<Sprite>();
            foreach (var (r, c) in coords)
            {
                var s = Get(map, prefix, r, c);
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }

        static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string normalized = folderPath.Replace('\\', '/').TrimEnd('/');
            int slash = normalized.LastIndexOf('/');
            string parent = normalized.Substring(0, slash);
            string child = normalized.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
