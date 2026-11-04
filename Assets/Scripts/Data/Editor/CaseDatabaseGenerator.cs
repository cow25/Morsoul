using System.IO;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Data.Editor
{
    /// <summary>
    /// Builds Assets/Resources/CaseDatabase.asset from every CaseData asset so
    /// the case loop can resolve Session.CurrentCase -> CaseData at runtime.
    /// Run after generating or adding case content (or use
    /// Morsoul > Setup > Rebuild Campaign Wiring to refresh hub entries too).
    /// </summary>
    public static class CaseDatabaseGenerator
    {
        const string DatabasePath = "Assets/Resources/CaseDatabase.asset";

        [MenuItem("Morsoul/Data/Rebuild Case Database")]
        public static void Rebuild()
        {
            var db = AssetDatabase.LoadAssetAtPath<CaseDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<CaseDatabase>();
                Directory.CreateDirectory("Assets/Resources");
                AssetDatabase.CreateAsset(db, DatabasePath);
            }

            db.cases.Clear();
            var found = new System.Collections.Generic.List<CaseData>();
            foreach (string guid in AssetDatabase.FindAssets("t:CaseData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<CaseData>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (data != null) found.Add(data);
            }
            found.Sort((a, b) => a.caseId.CompareTo(b.caseId));
            db.cases.AddRange(found);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"CaseDatabase: indexed {db.cases.Count} cases -> {DatabasePath}");
        }
    }
}
