using System.IO;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Data.Editor
{
    /// <summary>
    /// Builds Assets/Resources/ClueRegistry.asset from every ClueData asset
    /// in the project so runtime code can resolve clue ids without the
    /// asset database. Run after generating or editing case content.
    /// </summary>
    public static class ClueRegistryGenerator
    {
        const string RegistryPath = "Assets/Resources/ClueRegistry.asset";

        [MenuItem("Morsoul/Data/Rebuild Clue Registry")]
        public static void Rebuild()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ClueRegistry>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<ClueRegistry>();
                Directory.CreateDirectory("Assets/Resources");
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }

            registry.clues.Clear();
            int missingId = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ClueData"))
            {
                var clue = AssetDatabase.LoadAssetAtPath<ClueData>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (clue == null) continue;
                if (string.IsNullOrEmpty(clue.id)) missingId++;
                registry.clues.Add(clue);
            }

            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            Debug.Log($"ClueRegistry: indexed {registry.clues.Count} clues" +
                (missingId > 0 ? $" ({missingId} with empty ids — they cannot be restored by id)" : "") +
                $" -> {RegistryPath}");
        }
    }
}
