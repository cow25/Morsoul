using System;
using System.Text.RegularExpressions;
using Morsoul.Data;

namespace Morsoul.Core
{
    /// <summary>
    /// Formats ClueData text for UI Toolkit Labels without spoiling red herrings
    /// or solution links: substitutes {PlayerName}, neutralizes internal IDs/types,
    /// and styles key forensic phrases &amp; dissonance keywords for light or dark panels.
    /// </summary>
    public static class ClueHighlightFormatter
    {
        public const string LightAccentHex = "#8A3A0A";
        public const string DarkAccentHex = "#F4C46A";

        static readonly Regex BoldTagRegex = new Regex("<b>(.*?)</b>", RegexOptions.Singleline | RegexOptions.Compiled);

        public static string FriendlyCategory(ClueData clue)
        {
            if (clue == null) return "Evidence";
            return clue.type switch
            {
                ClueType.Physical => "Physical Evidence",
                ClueType.Testimony => "Witness Testimony",
                ClueType.Latent => "Runic Log",
                ClueType.RedHerring => "Observation",
                _ => "Evidence"
            };
        }

        public static string FriendlyTool(Tool tool) => tool switch
        {
            Tool.GolemEye => "Golem Eye",
            Tool.AetherSight => "Aether Sight",
            Tool.EmotionEcho => "Emotion Echo",
            _ => "Direct Inspection"
        };

        public static string FormatDescription(ClueData clue, string playerName, bool darkBackground = false)
        {
            if (clue == null || string.IsNullOrEmpty(clue.description)) return "";
            return FormatRawText(clue.description, clue.dissonanceKeyword, playerName, darkBackground);
        }

        public static string FormatRawText(string text, string keyword, string playerName, bool darkBackground = false)
        {
            if (string.IsNullOrEmpty(text)) return "";

            string resolvedName = string.IsNullOrWhiteSpace(playerName) ? "Golem" : playerName.Trim();
            string formatted = text.Replace("{PlayerName}", resolvedName);

            string hex = darkBackground ? DarkAccentHex : LightAccentHex;
            formatted = BoldTagRegex.Replace(formatted, m => $"<color={hex}><b>{m.Groups[1].Value}</b></color>");

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                int idx = formatted.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    bool alreadyTagged = idx >= 3 && formatted.Substring(Math.Max(0, idx - 3), 3) == "<b>";
                    if (!alreadyTagged)
                    {
                        string matched = formatted.Substring(idx, keyword.Length);
                        formatted = formatted.Remove(idx, keyword.Length)
                            .Insert(idx, $"<color={hex}><b>{matched}</b></color>");
                    }
                }
            }

            return formatted;
        }
    }
}

