using UnityEditor;
using UnityEngine;

namespace Morsoul.Hub.Editor
{
    public class HubArtAssetPostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetImporter.importSettingsMissing) return;
            var settings = HubArtImportSettings.Load();
            if (settings == null) return;
            var preset = settings.TexturePresetFor(assetPath);
            if (preset == null) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = preset.spritePixelsPerUnit;
            importer.filterMode = preset.filterMode;
            importer.mipmapEnabled = preset.mipmaps;
            importer.textureCompression = preset.compression;
        }

        void OnPreprocessAudio()
        {
            if (!assetImporter.importSettingsMissing) return;
            var settings = HubArtImportSettings.Load();
            if (settings == null) return;
            var preset = settings.AudioPresetFor(assetPath);
            if (preset == null) return;
            var importer = (AudioImporter)assetImporter;
            var sample = importer.defaultSampleSettings;
            sample.loadType = preset.loadType;
            sample.compressionFormat = preset.format;
            sample.quality = preset.quality;
            importer.defaultSampleSettings = sample;
        }
    }

    public static class HubArtFolderSetup
    {
        public static readonly string[] Folders =
        {
            "Assets/Art/Cutscenes",
            "Assets/Art/Portraits",
            "Assets/Art/Characters",
            "Assets/Art/Characters/Player/Body_A",
            "Assets/Art/Characters/NPCs",
            "Assets/Art/Environments",
            "Assets/Art/Environments/Tilesets",
            "Assets/Art/Environments/Buildings/Exterior",
            "Assets/Art/Environments/Buildings/Interior",
            "Assets/Art/Environments/Props/Static",
            "Assets/Art/Environments/Props/Trees",
            "Assets/Art/Environments/Props/Animated",
            "Assets/Art/Environments/Stations",
            "Assets/Art/Reference",
            "Assets/Art/UI",
            "Assets/Art/Profiles",
            "Assets/Tiles/Palettes",
            "Assets/Tiles/RuleTiles",
            "Assets/Tiles/StaticTiles",
            "Assets/Animations/Characters",
            "Assets/Animations/Environment",
            "Assets/Prefabs/Environment",
            "Assets/Audio/Music",
            "Assets/Audio/SFX",
            "Assets/Audio/VO",
        };

        [MenuItem("Morsoul/Art/Create Art & Audio Folders")]
        public static void CreateFolders()
        {
            foreach (var folder in Folders) EnsureFolder(folder);
            if (AssetDatabase.LoadAssetAtPath<HubArtImportSettings>(HubArtImportSettings.AssetPath) == null)
            {
                var settings = ScriptableObject.CreateInstance<HubArtImportSettings>();
                AssetDatabase.CreateAsset(settings, HubArtImportSettings.AssetPath);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("HubArtFolderSetup: art/audio folders and ArtImportSettings are ready.");
        }

        static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folderPath));
        }
    }
}
