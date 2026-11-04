using System.Collections.Generic;
using Morsoul.Data;

namespace Morsoul.Journal
{
    /// <summary>The four Clue Journal tabs, in header-row order (keys 1-4).</summary>
    public enum JournalTab
    {
        Physical = 0,
        Testimony = 1,
        Logs = 2,
        Dossier = 3,
    }

    /// <summary>
    /// Pure helpers for the Clue Journal overlay — tab filtering, dossier
    /// flag-gating, locked-log masking and card preview truncation. Kept
    /// UI-free so every rule is EditMode-testable (same approach as
    /// GateRule/HubTargeting in the hub assembly).
    /// </summary>
    public static class JournalFilter
    {
        /// <summary>Max characters of a clue description shown on a list card.</summary>
        public const int PreviewLength = 80;

        /// <summary>
        /// True when a clue of <paramref name="type"/> belongs on
        /// <paramref name="tab"/>. Red herrings count as physical evidence;
        /// latent clues are never listed (they are surfaced as runic logs).
        /// </summary>
        public static bool ForTab(ClueType type, JournalTab tab)
        {
            switch (tab)
            {
                case JournalTab.Physical:
                    return type == ClueType.Physical || type == ClueType.RedHerring;
                case JournalTab.Testimony:
                    return type == ClueType.Testimony;
                default:
                    return false;
            }
        }

        /// <summary>Collected clues that belong on the given tab, in order, nulls dropped.</summary>
        public static List<ClueData> CluesForTab(IEnumerable<ClueData> clues, JournalTab tab)
        {
            var result = new List<ClueData>();
            if (clues == null) return result;
            foreach (var clue in clues)
            {
                if (clue == null) continue;
                if (ForTab(clue.type, tab)) result.Add(clue);
            }
            return result;
        }

        /// <summary>
        /// A dossier entry is visible when it has no required flag, or when the
        /// flag is present in <paramref name="flags"/>.
        /// </summary>
        public static bool IsDossierVisible(string requiresFlag, ISet<string> flags)
        {
            if (string.IsNullOrEmpty(requiresFlag)) return true;
            return flags != null && flags.Contains(requiresFlag);
        }

        /// <summary>True when <paramref name="flag"/> is set and non-empty.</summary>
        public static bool HasFlag(ISet<string> flags, string flag)
        {
            return !string.IsNullOrEmpty(flag) && flags != null && flags.Contains(flag);
        }

        /// <summary>True when the log has been registered in the collected set.</summary>
        public static bool IsLogCollected(RunicLogData log, ICollection<RunicLogData> collected)
        {
            return log != null && collected != null && collected.Contains(log);
        }

        /// <summary>
        /// Scrambles locked text: every non-whitespace character becomes '?',
        /// preserving word lengths and line breaks so the card looks sealed
        /// without leaking content.
        /// </summary>
        public static string Mask(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsWhiteSpace(chars[i])) chars[i] = '?';
            return new string(chars);
        }

        /// <summary>First <paramref name="maxChars"/> characters, ellipsis appended when truncated.</summary>
        public static string Preview(string text, int maxChars = PreviewLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= maxChars) return text;
            return text.Substring(0, maxChars) + "…";
        }

        /// <summary>List-card title for a runic log asset.</summary>
        public static string LogTitle(RunicLogData log)
        {
            return log == null ? "Runic Log ?" : $"Runic Log {log.id}";
        }
    }
}
