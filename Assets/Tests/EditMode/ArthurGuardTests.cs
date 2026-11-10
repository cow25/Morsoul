using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Morsoul.Cutscene;
using Morsoul.Data;
using Morsoul.Dialogue;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Tests
{
    /// <summary>
    /// Lore guard: the name "Arthur" — the Golem's pre-cataclysm identity —
    /// must never appear in present-day, player-facing text. The test scans
    /// every DialogueData, CutsceneData, CaseData and ClueData asset under
    /// Assets/Data, collects every serialized string field, and asserts that
    /// none contains the whole word "Arthur" (regex \bArthur\b, ordinal,
    /// case-sensitive — "Arthurian" or "arthur" do not trip the guard).
    ///
    /// Legitimate exceptions, per the narrative doc:
    ///  - RunicLogData is skipped entirely (Rune-Cant logs are lore text).
    ///  - CutsceneData whose asset filename or id contains "Opening"
    ///    (pre-cataclysm flashback cutscenes, if any are added).
    ///  - CutsceneData whose asset filename or id contains "Ending" or
    ///    "Epilogue" (Ending 3 and the epilogue legitimately use the name).
    ///  - JournalData / epilogue strings are out of scope (not scanned).
    ///  - Any string field belonging to a dialogue node or cutscene shot
    ///    whose sibling speakerName / captionSpeaker field equals "Elise"
    ///    (her flashback voice is allowed to say the name).
    ///
    /// The guard is content-driven: if the content generators have not run
    /// and AssetDatabase.FindAssets returns nothing, the test still passes —
    /// it exists to catch future content, not to validate today's disk state.
    /// </summary>
    public class ArthurGuardTests
    {
        static readonly Regex ForbiddenName =
            new Regex(@"\bArthur\b", RegexOptions.CultureInvariant);

        static readonly string[] ScanFolders = { "Assets/Data" };

        static readonly string[] ScannedTypeFilters =
        {
            "t:DialogueData",
            "t:CutsceneData",
            "t:CaseData",
            "t:ClueData",
            "t:RunicLogData",
        };

        static readonly string[] ExemptCutsceneTokens =
        {
            "Opening",
            "Ending",
            "Epilogue",
        };

        [Test]
        public void PresentDayContent_DoesNotNameArthur()
        {
            var violations = new List<string>();
            foreach (string guid in FindScannedGuids())
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null)
                    continue;
                if (asset is RunicLogData)
                    continue; // Rune-Cant logs are lore — always allowed.
                if (asset is CutsceneData cutscene && IsExemptCutscene(cutscene, path))
                    continue;
                CollectViolations(asset, path, violations);
            }
            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        static IEnumerable<string> FindScannedGuids()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string filter in ScannedTypeFilters)
                foreach (string guid in AssetDatabase.FindAssets(filter, ScanFolders))
                    if (seen.Add(guid))
                        yield return guid;
        }

        static bool IsExemptCutscene(CutsceneData data, string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path) ?? "";
            string id = data.id ?? "";
            foreach (string token in ExemptCutsceneTokens)
                if (fileName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0
                    || id.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        static void CollectViolations(UnityEngine.Object asset, string path,
            List<string> violations)
        {
            // Flatten every serialized string field on the asset into
            // propertyPath -> value (nested serializable arrays included).
            var strings = new List<KeyValuePair<string, string>>();
            using (var serialized = new SerializedObject(asset))
            {
                var prop = serialized.GetIterator();
                while (prop.Next(true))
                    if (prop.propertyType == SerializedPropertyType.String)
                        strings.Add(new KeyValuePair<string, string>(
                            prop.propertyPath, prop.stringValue));
            }

            var byPath = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in strings)
                byPath[pair.Key] = pair.Value;

            foreach (var pair in strings)
            {
                string value = pair.Value;
                if (string.IsNullOrEmpty(value) || pair.Key == "m_Name")
                    continue; // m_Name is the asset identifier, not display text.
                if (!ForbiddenName.IsMatch(value))
                    continue;
                if (HasEliseSpeaker(byPath, pair.Key))
                    continue;
                violations.Add($"{path}: {pair.Key} names 'Arthur': {Snippet(value)}");
            }
        }

        /// <summary>
        /// Walks the ancestor element paths of a serialized string field
        /// (e.g. nodes.Array.data[2].options.Array.data[0].label) and returns
        /// true when a sibling speakerName / captionSpeaker equals "Elise" —
        /// the flashback voice that is allowed to say the name.
        /// </summary>
        static bool HasEliseSpeaker(IReadOnlyDictionary<string, string> strings,
            string propertyPath)
        {
            string ancestor = propertyPath;
            int dot;
            while ((dot = ancestor.LastIndexOf('.')) >= 0)
            {
                ancestor = ancestor.Substring(0, dot);
                if (IsElise(strings, ancestor + ".speakerName")
                    || IsElise(strings, ancestor + ".captionSpeaker"))
                    return true;
            }
            return false;
        }

        static bool IsElise(IReadOnlyDictionary<string, string> strings, string key)
        {
            return strings.TryGetValue(key, out string speaker)
                   && string.Equals(speaker, "Elise", StringComparison.Ordinal);
        }

        static string Snippet(string value)
        {
            string flat = value.Replace('\n', ' ').Replace('\r', ' ');
            return flat.Length <= 120 ? flat : flat.Substring(0, 120) + "...";
        }
    }
}
