using System.IO;
using Morsoul.Hub;
using Morsoul.Hub.Editor;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Dev.Editor
{
    public static class ProcessNewSprites
    {
        [MenuItem("Morsoul/Art/Process Characters And Path")]
        public static void Run()
        {
            string brainDir = @"C:\Users\Administrator\.gemini\antigravity\brain\b080a7e8-1f4d-49ea-8b25-7142f99a7454";
            
            // 1. Process Player
            string playerJpg = Path.Combine(brainDir, "player_golem_sprite_1794301473748.jpg");
            ProcessSprite(playerJpg, "Assets/Art/Sprites/Characters/Player.png", 32, 40, "Assets/Art/Profiles/Player.asset");

            // 2. Process Eliza
            string elizaJpg = Path.Combine(brainDir, "eliza_astra_sprite_1794301490607.jpg");
            ProcessSprite(elizaJpg, "Assets/Art/Sprites/Characters/Eliza.png", 28, 38, "Assets/Art/Profiles/Eliza.asset");

            // 3. Process PathDirt
            string pathJpg = Path.Combine(brainDir, "prop_pathdirt_1794301477881.jpg");
            ProcessSprite(pathJpg, "Assets/Art/Sprites/Props/Prop_PathDirt.png", 32, 32, "Assets/Art/Profiles/Prop_PathDirt.asset");

            // Re-dress scenes with the updated ground and path
            HubDressingBuilder.DressAll();

            Debug.Log("ProcessNewSprites: Characters and path sprites created, bound to profiles, and scenes redressed!");
        }

        static void ProcessSprite(string sourceJpg, string outPngPath, int targetW, int targetH, string profilePath)
        {
            if (!File.Exists(sourceJpg))
            {
                Debug.LogWarning($"ProcessNewSprites: source not found at {sourceJpg}");
                return;
            }

            byte[] data = File.ReadAllBytes(sourceJpg);
            var src = new Texture2D(2, 2);
            src.LoadImage(data);

            // Find bounds where pixels are not white
            int minX = src.width, maxX = 0, minY = src.height, maxY = 0;
            for (int y = 0; y < src.height; y++)
            {
                for (int x = 0; x < src.width; x++)
                {
                    var p = src.GetPixel(x, y);
                    if (p.r < 0.93f || p.g < 0.93f || p.b < 0.93f)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (minX >= maxX || minY >= maxY)
            {
                minX = 0; maxX = src.width - 1;
                minY = 0; maxY = src.height - 1;
            }

            int cropW = maxX - minX + 1;
            int cropH = maxY - minY + 1;

            var target = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);

            for (int ty = 0; ty < targetH; ty++)
            {
                float normY = (float)ty / (targetH - 1);
                int sy = minY + Mathf.RoundToInt(normY * (cropH - 1));
                for (int tx = 0; tx < targetW; tx++)
                {
                    float normX = (float)tx / (targetW - 1);
                    int sx = minX + Mathf.RoundToInt(normX * (cropW - 1));
                    var p = src.GetPixel(sx, sy);
                    if (p.r > 0.91f && p.g > 0.91f && p.b > 0.91f)
                    {
                        target.SetPixel(tx, ty, new Color(0, 0, 0, 0));
                    }
                    else
                    {
                        target.SetPixel(tx, ty, new Color(p.r, p.g, p.b, 1f));
                    }
                }
            }
            target.Apply();

            string dir = Path.GetDirectoryName(outPngPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(outPngPath, target.EncodeToPNG());
            AssetDatabase.ImportAsset(outPngPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(outPngPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 32;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outPngPath);
            if (sprite != null && !string.IsNullOrEmpty(profilePath))
            {
                var profile = AssetDatabase.LoadAssetAtPath<VisualProfile>(profilePath);
                if (profile != null)
                {
                    profile.sprite = sprite;
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"ProcessNewSprites: bound {sprite.name} to {profile.name}");
                }
            }
        }
    }
}
// Domain reload trigger

