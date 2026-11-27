using System;
using System.Collections.Generic;
using UnityEngine;

namespace Morsoul.Core
{
    /// <summary>
    /// Persists unlocked endings across playthroughs for the Title Screen Ending Gallery.
    /// </summary>
    public static class EndingGalleryStore
    {
        const string PrefKey = "Morsoul.UnlockedEndings";

        public readonly struct EndingInfo
        {
            public readonly int Id;
            public readonly string Title;
            public readonly string Description;

            public EndingInfo(int id, string title, string description)
            {
                Id = id;
                Title = title;
                Description = description;
            }
        }

        public static readonly EndingInfo[] AllEndings =
        {
            new EndingInfo(
                1,
                "Ending I — Fractured Circuit",
                "Your deduction collapsed under strain or mistrial. Without the true chain, the truth behind Morsoul's resonance remains buried."),
            new EndingInfo(
                2,
                "Ending II — Partial Awakening",
                "You solved the final case and confronted the core anomaly, though lingering runic logs left deeper questions unanswered."),
            new EndingInfo(
                3,
                "Ending III — Complete Resonance",
                "With every runic log recovered and the full causal chain intact, you uncovered the complete truth of the Guild and the ruins."),
        };

        public static void RecordUnlocked(int endingId)
        {
            if (endingId < 1 || endingId > 3) return;
            var set = LoadSet();
            if (!set.Add(endingId)) return;
            var list = new List<int>(set);
            list.Sort();
            PlayerPrefs.SetString(PrefKey, string.Join(",", list));
            PlayerPrefs.Save();
        }

        public static bool IsUnlocked(int endingId) => LoadSet().Contains(endingId);

        public static int UnlockedCount => LoadSet().Count;

        public static void ClearAll()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }

        static HashSet<int> LoadSet()
        {
            var set = new HashSet<int>();
            string raw = PlayerPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(raw)) return set;
            foreach (var part in raw.Split(','))
                if (int.TryParse(part.Trim(), out int id) && id >= 1 && id <= 3)
                    set.Add(id);
            return set;
        }
    }
}

