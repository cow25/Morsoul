using System;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Hub.Editor
{
    public class HubArtImportSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/Art/ArtImportSettings.asset";

        [Serializable]
        public class TexturePreset
        {
            public int spritePixelsPerUnit = 32;
            public FilterMode filterMode = FilterMode.Point;
            public bool mipmaps;
            public TextureImporterCompression compression = TextureImporterCompression.Uncompressed;
        }

        [Serializable]
        public class AudioPreset
        {
            public AudioClipLoadType loadType = AudioClipLoadType.DecompressOnLoad;
            public AudioCompressionFormat format = AudioCompressionFormat.Vorbis;
            [Range(0f, 1f)] public float quality = 0.7f;
        }

        public TexturePreset characters = new TexturePreset();
        public TexturePreset environments = new TexturePreset();
        public TexturePreset ui = new TexturePreset();
        public TexturePreset portraits = new TexturePreset
        {
            spritePixelsPerUnit = 100,
            filterMode = FilterMode.Bilinear,
            compression = TextureImporterCompression.Compressed,
        };
        public TexturePreset cutscenes = new TexturePreset
        {
            spritePixelsPerUnit = 100,
            filterMode = FilterMode.Bilinear,
            compression = TextureImporterCompression.Compressed,
        };

        public AudioPreset music = new AudioPreset { loadType = AudioClipLoadType.Streaming };
        public AudioPreset sfx = new AudioPreset();
        public AudioPreset vo = new AudioPreset { loadType = AudioClipLoadType.CompressedInMemory };

        public TexturePreset TexturePresetFor(string assetPath)
        {
            if (assetPath.StartsWith("Assets/Art/Characters/")) return characters;
            if (assetPath.StartsWith("Assets/Art/Environments/")) return environments;
            if (assetPath.StartsWith("Assets/Art/UI/")) return ui;
            if (assetPath.StartsWith("Assets/Art/Portraits/")) return portraits;
            if (assetPath.StartsWith("Assets/Art/Cutscenes/")) return cutscenes;
            return null;
        }

        public AudioPreset AudioPresetFor(string assetPath)
        {
            if (assetPath.StartsWith("Assets/Audio/Music/")) return music;
            if (assetPath.StartsWith("Assets/Audio/SFX/")) return sfx;
            if (assetPath.StartsWith("Assets/Audio/VO/")) return vo;
            return null;
        }

        public static HubArtImportSettings Load() =>
            AssetDatabase.LoadAssetAtPath<HubArtImportSettings>(AssetPath);
    }
}
