using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Morsoul.Hub.Editor
{
    /// <summary>
    /// Shared Editor helpers for constructing Pixel Crawler 2D hub scenes:
    /// Tilemap grids, batch RuleTile painting, Y-sorted sprite props, and
    /// animated character VisualProfile wiring.
    /// </summary>
    public static class PixelCrawlerHubToolkit
    {
        const string RuleTilesRoot = "Assets/Tiles/RuleTiles";
        const string ProfilesRoot = "Assets/Art/Profiles";

        static readonly Dictionary<string, Dictionary<string, Sprite>> SheetCache =
            new Dictionary<string, Dictionary<string, Sprite>>(StringComparer.Ordinal);

        public static void ClearCache() => SheetCache.Clear();

        public static RuleTile LoadRuleTile(string name)
        {
            var rt = AssetDatabase.LoadAssetAtPath<RuleTile>($"{RuleTilesRoot}/{name}.asset");
            if (rt == null)
                Debug.LogWarning($"PixelCrawlerHubToolkit: RuleTile '{name}' not found under '{RuleTilesRoot}'.");
            return rt;
        }

        public static TileBase LoadStaticTile(string category, string tileName)
        {
            return AssetDatabase.LoadAssetAtPath<TileBase>($"Assets/Tiles/StaticTiles/{category}/{tileName}.asset");
        }

        public static Sprite LoadSprite(string sheetPath, string spriteName)
        {
            if (!SheetCache.TryGetValue(sheetPath, out var dict))
            {
                dict = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
                {
                    if (obj is Sprite s) dict[s.name] = s;
                }
                SheetCache[sheetPath] = dict;
            }
            dict.TryGetValue(spriteName, out var found);
            if (found == null)
                Debug.LogWarning($"PixelCrawlerHubToolkit: Sprite '{spriteName}' not found at '{sheetPath}'.");
            return found;
        }

        public static Grid EnsureGrid(Scene scene, string rootName = "EnvironmentGrid")
        {
            var existing = FindRoot(scene, rootName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

            var go = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(go, scene);
            var grid = go.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            return grid;
        }

        public static Tilemap EnsureTilemapLayer(
            Transform gridParent, string layerName, int sortingOrder, float zOffset, bool addCollider = false)
        {
            var existing = gridParent.Find(layerName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var go = new GameObject(layerName);
            go.transform.SetParent(gridParent, false);
            go.transform.localPosition = new Vector3(0f, 0f, zOffset);

            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;

            if (addCollider)
                go.AddComponent<TilemapCollider2D>();

            return tilemap;
        }

        public static void FillRect(Tilemap map, TileBase tile, int xMin, int yMin, int xMax, int yMax)
        {
            if (map == null || tile == null || xMax < xMin || yMax < yMin) return;
            int w = xMax - xMin + 1;
            int h = yMax - yMin + 1;
            var tiles = new TileBase[w * h];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = tile;
            map.SetTilesBlock(new BoundsInt(xMin, yMin, 0, w, h, 1), tiles);
        }

        public static void ClearRect(Tilemap map, int xMin, int yMin, int xMax, int yMax)
        {
            if (map == null || xMax < xMin || yMax < yMin) return;
            int w = xMax - xMin + 1;
            int h = yMax - yMin + 1;
            var empty = new TileBase[w * h];
            map.SetTilesBlock(new BoundsInt(xMin, yMin, 0, w, h, 1), empty);
        }

        public static void FillRandomStaticBlock(
            Tilemap map, string sheetName, int xMin, int yMin, int xMax, int yMax,
            int rowMin, int rowMax, int colMin, int colMax)
        {
            if (map == null || xMax < xMin || yMax < yMin) return;
            var pool = new System.Collections.Generic.List<TileBase>();
            for (int r = rowMin; r <= rowMax; r++)
                for (int c = colMin; c <= colMax; c++)
                {
                    var t = LoadStaticTile(sheetName, $"{sheetName}_{r}_{c}");
                    if (t != null) pool.Add(t);
                }
            if (pool.Count == 0) return;
            for (int y = yMin; y <= yMax; y++)
                for (int x = xMin; x <= xMax; x++)
                {
                    int idx = Mathf.Abs((x * 73856093) ^ (y * 19349663)) % pool.Count;
                    map.SetTile(new Vector3Int(x, y, 0), pool[idx]);
                }
        }

        public static Transform EnsurePropsRoot(Scene scene, string rootName = "EnvironmentProps")
        {
            var existing = FindRoot(scene, rootName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            var go = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.transform;
        }

        public static GameObject PlaceSpriteProp(
            Transform parent, string name, Sprite sprite, Vector2 pos,
            float zBias = -0.005f, Vector2? colliderSize = null, Vector2? colliderOffset = null,
            bool flipX = false, int sortingOrder = 0, Color? tint = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, pos.y, pos.y * YSort2D.DepthPerUnitY + zBias);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = flipX;
            sr.sortingOrder = sortingOrder;
            if (tint.HasValue) sr.color = tint.Value;

            if (colliderSize.HasValue)
            {
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = false;
                col.size = colliderSize.Value;
                if (colliderOffset.HasValue) col.offset = colliderOffset.Value;
            }
            return go;
        }

        const string HandsSheetPath = "Assets/Art/Characters/Weapons/Hands.png";

        public static VisualProfile EnsureCharacterProfile(
            string profileName, string sheetPath, string idleSpriteName,
            string controllerPath, Vector2 visualOffset, bool flipX = false,
            VisualAttachment[] attachments = null, Color? tint = null)
        {
            EnsureFolder(ProfilesRoot);
            string assetPath = $"{ProfilesRoot}/{profileName}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VisualProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VisualProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            profile.sprite = LoadSprite(sheetPath, idleSpriteName);
            profile.animatorController = string.IsNullOrEmpty(controllerPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            profile.tint = tint ?? Color.white;
            profile.visualOffset = visualOffset;
            profile.visualScale = Vector2.one;
            profile.flipX = flipX;
            profile.sortingOrder = 0;
            profile.attachments = attachments ?? BuildDefaultAttachments(sheetPath);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static VisualAttachment[] BuildDefaultAttachments(string sheetPath)
        {
            if (string.IsNullOrEmpty(sheetPath)) return null;

            // Pixel Crawler's Knight, Wizard, and Rogue base sheets only include sleeve/shoulder
            // cuffs; modular hands come from Weapons/Hands.png (empty-handed, no held items),
            // tucked tightly at sleeve cuffs in unflipped (facing-right) local space.
            if (sheetPath.IndexOf("/NPCs/Wizard/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MakeHandPair(
                    LoadSprite(HandsSheetPath, "Hands_00"), new Vector2(-0.27f, 0.45f),
                    LoadSprite(HandsSheetPath, "Hands_02"), new Vector2(0.29f, 0.48f));
            }

            if (sheetPath.IndexOf("/NPCs/Knight/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MakeHandPair(
                    LoadSprite(HandsSheetPath, "Hands_06"), new Vector2(-0.28f, 0.44f),
                    LoadSprite(HandsSheetPath, "Hands_07"), new Vector2(0.26f, 0.44f));
            }

            if (sheetPath.IndexOf("/NPCs/Rogue/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MakeHandPair(
                    LoadSprite(HandsSheetPath, "Hands_04"), new Vector2(-0.28f, 0.45f),
                    LoadSprite(HandsSheetPath, "Hands_05"), new Vector2(0.26f, 0.45f));
            }

            return null;
        }

        static VisualAttachment[] MakeHandPair(Sprite backHand, Vector2 backPos, Sprite frontHand, Vector2 frontPos) =>
            new[]
            {
                new VisualAttachment
                {
                    id = "BackHand", sprite = backHand,
                    localOffset = backPos, localZ = -0.001f,
                    localScale = Vector2.one,
                },
                new VisualAttachment
                {
                    id = "FrontHand", sprite = frontHand,
                    localOffset = frontPos, localZ = -0.002f,
                    localScale = Vector2.one,
                },
            };

        public static void BindActorVisual(GameObject actor, VisualProfile profile, bool addYSort = true)
        {
            if (actor == null) return;
            if (addYSort)
            {
                if (actor.GetComponent<YSort2D>() == null)
                    actor.AddComponent<YSort2D>();
                var p = actor.transform.position;
                actor.transform.position = new Vector3(p.x, p.y, p.y * YSort2D.DepthPerUnitY);
            }

            var visual = actor.GetComponent<EntityVisual>();
            if (visual == null) visual = actor.AddComponent<EntityVisual>();
            visual.profile = profile;
            visual.Apply(profile);
            EditorUtility.SetDirty(actor);
            EditorUtility.SetDirty(visual);
            if (visual.Renderer != null) EditorUtility.SetDirty(visual.Renderer);
            if (visual.Animator != null) EditorUtility.SetDirty(visual.Animator);
        }

        public static void BindRoot(Scene scene, string rootName, VisualProfile profile)
        {
            var go = FindRoot(scene, rootName);
            if (go != null) BindActorVisual(go, profile, addYSort: true);
        }

        public static void BindStaticProp(Scene scene, string rootName, string profileName,
            string sheetPath, string spriteName, Vector2? offset = null)
        {
            var go = FindRoot(scene, rootName);
            if (go == null) return;
            var profile = EnsureCharacterProfile(
                profileName, sheetPath, spriteName, null, offset ?? Vector2.zero, flipX: false);
            BindActorVisual(go, profile, addYSort: true);
        }

        public static void HideTriggerVisual(Scene scene, string rootName)
        {
            var go = FindRoot(scene, rootName);
            if (go == null) return;
            var ev = go.GetComponent<EntityVisual>();
            if (ev != null) ev.Apply(null);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
        }

        public static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var c in UnityEngine.Object.FindObjectsByType<T>())
                if (c != null && c.gameObject.scene == scene) return c;
            return null;
        }

        public static void RebuildBounds(Scene scene, string rootName, params (string name, Vector2 pos, Vector2 size)[] boxes)
        {
            var old = FindRoot(scene, rootName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            foreach (var (name, pos, size) in boxes)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(pos.x, pos.y, 0f);
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = false;
                col.size = size;
            }
        }

        public static void ConfigureCamera(Scene scene, Vector2 boundsMin, Vector2 boundsMax, float orthoSize, Color bgColor)
        {
            var cam = FindInScene<CameraFollow2D>(scene);
            if (cam == null) return;
            cam.useBounds = true;
            cam.boundsMin = boundsMin;
            cam.boundsMax = boundsMax;
            var c = cam.GetComponent<Camera>();
            if (c != null)
            {
                c.orthographicSize = orthoSize;
                c.backgroundColor = bgColor;
            }
        }

        public static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        public static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string normalized = folderPath.Replace('\\', '/').TrimEnd('/');
            int slash = normalized.LastIndexOf('/');
            EnsureFolder(normalized.Substring(0, slash));
            AssetDatabase.CreateFolder(normalized.Substring(0, slash), normalized.Substring(slash + 1));
        }
    }
}

