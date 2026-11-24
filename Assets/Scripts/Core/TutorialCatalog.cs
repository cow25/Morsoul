using System;
using System.Collections.Generic;
using UnityEngine;

namespace Morsoul.Core
{
    public enum TutorialCategory
    {
        MovementAndControls,
        InvestigationAndJournal,
        DeductionBoard,
        FocusAndRest,
        Abilities
    }

    public sealed class TutorialEntry
    {
        public string Id;
        public TutorialCategory Category;
        public string Title;
        public string Keybinds;
        public string Summary;
        public string[] Steps;
        public bool AlwaysUnlockedInLogs;
    }

    /// <summary>
    /// Single source of truth for contextual tutorials and Escape Menu Tutorial Logs.
    /// Persistence uses SessionState flags ("Tutorial.Seen.<id>") plus a global
    /// PlayerPrefs toggle for experienced players.
    /// </summary>
    public static class TutorialCatalog
    {
        public const string SeenFlagPrefix = "Tutorial.Seen.";
        public const string SuppressAutoPrefKey = "Morsoul.SuppressAutoTutorials";

        static readonly TutorialEntry[] Entries =
        {
            new TutorialEntry
            {
                Id = "TUT_MOVE_3D",
                Category = TutorialCategory.MovementAndControls,
                Title = "3D Exploration Movement",
                Keybinds = "[WASD] / Arrows  •  [Left Shift] Brisk Walk",
                Summary = "Move through 3D investigation scenes relative to where you are looking.",
                AlwaysUnlockedInLogs = true,
                Steps = new[]
                {
                    "Use [W][A][S][D] or Arrow Keys to move around the chamber.",
                    "Hold [Left Shift] while moving to walk briskly across larger ruins or halls.",
                    "In 3D zones, aim with your Mouse and walk close to examine physical objects."
                }
            },
            new TutorialEntry
            {
                Id = "TUT_MOVE_2D",
                Category = TutorialCategory.MovementAndControls,
                Title = "2D Hub Navigation & Interaction",
                Keybinds = "[WASD] Move  •  [Left Shift] Sprint  •  [E] Interact",
                Summary = "District hubs use top-down movement instead of camera look.",
                AlwaysUnlockedInLogs = true,
                Steps = new[]
                {
                    "Use [W][A][S][D] or Arrow Keys to walk smoothly in any direction across 2D hubs.",
                    "Hold [Left Shift] to sprint quickly between witnesses, benches, and Carriers.",
                    "When an [E] prompt appears near an NPC, Carrier, Quest Board, or Bench, press [E] to interact."
                }
            },
            new TutorialEntry
            {
                Id = "TUT_MOUSE_3D",
                Category = TutorialCategory.MovementAndControls,
                Title = "3D Mouse Look & Object Inspection",
                Keybinds = "[Mouse] Look  •  [L-Click] Inspect  •  [Drag / Scroll] Examine",
                Summary = "Point your center reticle at clues or doors and click to inspect them up close.",
                AlwaysUnlockedInLogs = true,
                Steps = new[]
                {
                    "Move the Mouse to look around freely in 3D investigations (unlike 2D hubs).",
                    "Align the white center reticle with a clue and [Left-Click] to open the 3D Inspect Viewer.",
                    "Inside Inspect: hold [Left-Drag] to rotate the object, [Mouse Wheel] to zoom, and press [ESC] to close before opening menus."
                }
            },
            new TutorialEntry
            {
                Id = "TUT_MOUSE_UI",
                Category = TutorialCategory.MovementAndControls,
                Title = "Menus & Interface Controls",
                Keybinds = "[Mouse Click] Select  •  [G] Journal  •  [H] Board  •  [ESC] Pause / Back",
                Summary = "All overlays support full mouse interaction alongside quick keyboard hotkeys.",
                AlwaysUnlockedInLogs = true,
                Steps = new[]
                {
                    "When Dialogue, Journal [G], Deduction Board [H], Travel Menu, or Pause [ESC] opens, your cursor unlocks automatically.",
                    "Click buttons, evidence tabs, or destination entries directly, or use the shown keybind shortcuts.",
                    "Press [ESC] to close most active overlays cleanly (during cutscenes, hold [ESC] to skip)."
                }
            },
            new TutorialEntry
            {
                Id = "TUT_EYE_SCAN",
                Category = TutorialCategory.FocusAndRest,
                Title = "Golem Eye Scan & Focus Energy",
                Keybinds = "Hold [Tab] Scan  •  Release [Tab] Stop",
                Summary = "Your Golem Eye reveals hidden clues and chemical/runic traces, but drains Focus over time.",
                AlwaysUnlockedInLogs = true,
                Steps = new[]
                {
                    "Hold [Tab] to pulse your Golem Eye wave across the room; release [Tab] once targets glow.",
                    "Focus Energy (top-left bar) is shared across scenes and does NOT auto-refill for free.",
                    "Avoid holding [Tab] continuously—prolonged scanning drains 8 Focus/sec. Running empty weakens Focus discipline."
                }
            },
            new TutorialEntry
            {
                Id = "TUT_INVESTIGATE",
                Category = TutorialCategory.InvestigationAndJournal,
                Title = "Clues, Key Highlights & Journal",
                Keybinds = "[G] Open Journal  •  [1–4 / Q / E] Tabs  •  [W/S] Select  •  [Enter/R] Reinspect",
                Summary = "Collect physical evidence, compare testimonies, and reinspect clues from your Journal.",
                AlwaysUnlockedInLogs = true,
                Steps = new[]
                {
                    "Click highlighted objects to log them into your Journal (items taken for analysis move safely into Physical notes).",
                    "Open the Journal [G] anytime to review Physical clues, Testimonies, Runic Logs, and Dossiers.",
                    "Select a discovered clue and press [Enter], [R], double-click, or click [Inspect Model] to reopen its 3D/2D inspect view."
                }
            },
            new TutorialEntry
            {
                Id = "TUT_BOARD_BASICS",
                Category = TutorialCategory.DeductionBoard,
                Title = "Deduction Board: Directed Reasoning",
                Keybinds = "[H] Open Board  •  [Drag] Move Card  •  [Click→Click] Link  •  [Space] Submit",
                Summary = "Build a directional chain from foundational cause/testimony to the concluding proof.",
                AlwaysUnlockedInLogs = true,
                Steps = new[]
                {
                    "Click any card to read its full highlighted clue notes in the lower-right Evidence Inspector without leaving the Board.",
                    "Drag cards freely to organize, or click [Auto-Align].",
                    "Links are DIRECTED (Cause → Effect): click a source clue, then click its target clue (or Right-Drag / Shift-Drag). Right-click a link row in the ledger to sever it cleanly.",
                    "Select an unlocked Conclusion on the right and press [Space] or [Submit Conclusion] once your chain supports it."
                }
            },
            new TutorialEntry
            {
                Id = "TUT_BOARD_LOGIC",
                Category = TutorialCategory.DeductionBoard,
                Title = "Logical Deduction vs Guessing",
                Keybinds = "Check Lamp & Clue Details Before Linking",
                Summary = "Unsupported links drain Focus Energy (-15 per error; -25 in Case 5) across all cases, including Prologue.",
                AlwaysUnlockedInLogs = false,
                Steps = new[]
                {
                    "False links penalize your Focus bar immediately in every case (including Prologue). In Cases 1–5, hint tiers at 5, 10, and 20 errors also restore +20 Focus once each.",
                    "If Focus drops too low for an error, all board links clear and linking locks until you recover to 30 Focus.",
                    "Read clue mechanisms and witness contradictions carefully in your Journal [G] or Card Inspector instead of brute-forcing pairs."
                }
            },
            new TutorialEntry
            {
                Id = "TUT_REST_SPOT",
                Category = TutorialCategory.FocusAndRest,
                Title = "Rest Spots & Recovery Limits",
                Keybinds = "[E] Rest at Bench / Campfire",
                Summary = "Rest spots refill your Focus Energy to full after confirmation, with strict per-case limits.",
                AlwaysUnlockedInLogs = true,
                Steps = new[]
                {
                    "Approach a Rest Bench or Campfire, press [E], and confirm to rest until Focus Energy is completely full (10/sec).",
                    "Benches allow 2 Rests per active case with a 30s cooldown—movement is locked until resting finishes.",
                    "If you exhaust all rests and drop below 30 Focus, interacting offers an Emergency Reboot (wipes current board links and forfeits Clean grade)."
                }
            },
            new TutorialEntry
            {
                Id = "TUT_AETHER_COMBO",
                Category = TutorialCategory.Abilities,
                Title = "Aether Sight & District Backtracking",
                Keybinds = "[1] Toggle Aether Sight  •  [1] + [Tab] Dual Scan  •  Carrier [E]",
                Summary = "Reveal ancient First Age Runic Logs—including deeper secrets in earlier districts.",
                AlwaysUnlockedInLogs = false,
                Steps = new[]
                {
                    "Once unlocked, press [1] in 3D investigations to toggle Aether Sight (drains 3 Focus/sec while active; press [1] again to save Focus).",
                    "Some Runic Logs require BOTH Aether Sight [1] and Golem Eye [Tab] active together.",
                    "Use Carriers [E] in 2D hubs to revisit earlier case sites (Vault, Belfry, Whisperwood) and claim missed Runic Logs."
                }
            },
            new TutorialEntry
            {
                Id = "TUT_INTERROGATION",
                Category = TutorialCategory.Abilities,
                Title = "Interrogation & Emotion Echo",
                Keybinds = "[1] Press Claim  •  [2] Contradict  •  [3] Present Evidence  •  [E] Continue",
                Summary = "Expose contradictions in a suspect's testimony without exhausting your 3 Judicial Seals.",
                AlwaysUnlockedInLogs = false,
                Steps = new[]
                {
                    "Start each statement by clicking an underlined claim (or pressing [1] PRESS) to force elaboration and read Emotion Echo auras.",
                    "AMBER Aura signals a factual lie — counter with [2] CONTRADICT. VIOLET Aura signals guarded motive — probe with [3] PRESENT EVIDENCE.",
                    "Read evidence card descriptions carefully in the tray before submitting. Losing all 3 Credibility Seals triggers a Mistrial."
                }
            }
        };

        public static IReadOnlyList<TutorialEntry> All => Entries;

        public static TutorialEntry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Entries.Length; i++)
                if (string.Equals(Entries[i].Id, id, StringComparison.Ordinal))
                    return Entries[i];
            return null;
        }

        public static string CategoryDisplayName(TutorialCategory category) => category switch
        {
            TutorialCategory.MovementAndControls => "Movement & Controls",
            TutorialCategory.InvestigationAndJournal => "Investigation & Journal",
            TutorialCategory.DeductionBoard => "Deduction Board",
            TutorialCategory.FocusAndRest => "Focus & Rest Spots",
            TutorialCategory.Abilities => "Special Abilities",
            _ => "General"
        };

        public static string SeenFlag(string id) => SeenFlagPrefix + id;

        public static bool IsSeen(SessionState session, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return session != null && session.HasFlag(SeenFlag(id));
        }

        public static bool IsUnlockedInLogs(SessionState session, TutorialEntry entry)
        {
            if (entry == null) return false;
            if (entry.AlwaysUnlockedInLogs) return true;
            return IsSeen(session, entry.Id);
        }

        public static void MarkSeen(SessionState session, string id)
        {
            if (session != null && !string.IsNullOrEmpty(id))
                session.SetFlag(SeenFlag(id), true);
        }

        public static bool AutoTutorialsSuppressed
        {
            get => PlayerPrefs.GetInt(SuppressAutoPrefKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(SuppressAutoPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}

