using System.Collections.Generic;
using Morsoul.Data;
using Morsoul.Engine;

namespace Morsoul.Core
{
    /// <summary>
    /// Maps the save record to the canonical payload tree hashed and sealed
    /// by Algorithm 4. Every persisted field must be represented here —
    /// anything unmapped could be edited on disk without detection.
    /// </summary>
    public static class SaveCanonicalMap
    {
        /// <summary>
        /// Chooses the canonical shape that sealed the stored version. v2
        /// files must verify against their original map before migration;
        /// v3 files use the complete persisted-field map below.
        /// </summary>
        public static Dictionary<string, object> ToCanonicalForVerification(
            SaveState s, bool legacyStrainedScanUsed)
        {
            if (s == null || s.saveVersion >= 3) return ToCanonical(s);
            return new Dictionary<string, object>
            {
                ["saveVersion"] = s.saveVersion,
                ["profileId"] = s.profileId ?? "",
                ["playerName"] = s.playerName ?? "",
                ["currentCase"] = s.currentCase,
                ["checkpointSceneName"] = s.checkpointSceneName ?? "",
                ["collectedClueIds"] = new List<object>(Strings(s.collectedClueIds)),
                ["boardEdges"] = new List<object>(Strings(s.boardEdges)),
                ["runicLogsCollected"] = new List<object>(Bools(s.runicLogsCollected)),
                ["caseGrades"] = Grades(s.caseGrades),
                ["uneaseFlags"] = Flags(s.uneaseFlags),
                ["toolsUnlocked"] = Tools(s.toolsUnlocked),
                ["focus"] = new Dictionary<string, object>
                {
                    ["energy"] = s.focusEnergy,
                    ["maximum"] = s.focusMax,
                    ["restElapsedRemainderSeconds"] = s.restElapsedRemainderSeconds,
                    ["closedAtUtc"] = s.closedAtUtc ?? "",
                    ["lastOfflineRecoveryAppliedAtUtc"] = s.lastOfflineRecoveryAppliedAtUtc ?? "",
                    ["strainedScanUsed"] = legacyStrainedScanUsed,
                },
                ["boardStates"] = BoardStates(s.boardStates, includeV3Fields: false),
            };
        }

        public static Dictionary<string, object> ToCanonical(SaveState s)
        {
            var map = new Dictionary<string, object>
            {
                ["saveVersion"] = s.saveVersion,
                ["profileId"] = s.profileId ?? "",
                ["playerName"] = s.playerName ?? "",
                ["currentCase"] = s.currentCase,
                ["checkpointSceneName"] = s.checkpointSceneName ?? "",
                ["currentSceneMode"] = s.currentSceneMode,
                ["currentSceneId"] = s.currentSceneId ?? "",
                ["playerPosition"] = PlayerPosition(s.playerPosition),
                ["playerFacing2D"] = s.playerFacing2D,
                ["collectedClueIds"] = new List<object>(Strings(s.collectedClueIds)),
                ["boardEdges"] = new List<object>(Strings(s.boardEdges)),
                ["runicLogsCollected"] = new List<object>(Bools(s.runicLogsCollected)),
                ["caseGrades"] = Grades(s.caseGrades),
                ["uneaseFlags"] = Flags(s.uneaseFlags),
                ["toolsUnlocked"] = Tools(s.toolsUnlocked),
                ["focusState"] = new Dictionary<string, object>
                {
                    ["currentEnergy"] = s.focusEnergy,
                    ["maximumEnergy"] = s.focusMax,
                    ["restElapsedRemainder"] = s.restElapsedRemainderSeconds,
                    ["closedAtUtc"] = s.closedAtUtc ?? "",
                    ["lastTrustedUtc"] = s.lastTrustedUtc ?? "",
                    ["lastOfflineRecoveryAppliedAtUtc"] = s.lastOfflineRecoveryAppliedAtUtc ?? "",
                    ["strainedScanUsedByCase"] = new List<object>(Bools(s.strainedScanUsedByCase)),
                },
                ["boardStates"] = BoardStates(s.boardStates, includeV3Fields: true),
                ["caseGradeStates"] = CaseGradeStates(s.caseGradeStates),
                ["prologueCompleted"] = s.prologueCompleted,
                ["prologueGateUnlocked"] = s.prologueGateUnlocked,
                ["prologueRobeCollected"] = s.prologueRobeCollected,
                ["prologueExitLoaded"] = s.prologueExitLoaded,
                ["elizaIntroTriggered"] = s.elizaIntroTriggered,
                ["elizaIntroSeen"] = s.elizaIntroSeen,
                ["easternRouteUnlocked"] = s.easternRouteUnlocked,
                ["activeCutsceneId"] = s.activeCutsceneId ?? "",
                ["preCutsceneCheckpointId"] = s.preCutsceneCheckpointId ?? "",
                ["activeInterrogation"] = ActiveInterrogation(s.activeInterrogation),
            };
            if (s.saveVersion >= 4)
            {
                map["hasPlayerPosition"] = s.hasPlayerPosition;
                map["hasActiveInterrogation"] = s.hasActiveInterrogation;
                map["hasInvestigationReturn"] = s.hasInvestigationReturn;
                map["investigationCaseId"] = s.investigationCaseId;
                map["investigationReturn"] = s.investigationReturn == null ? null : new Dictionary<string, object>
                {
                    ["sceneName"] = s.investigationReturn.sceneName ?? "",
                    ["position"] = PlayerPosition(s.investigationReturn.position),
                    ["facing"] = s.investigationReturn.facing,
                };
            }
            return map;
        }

        static IEnumerable<string> Strings(List<string> list) => list ?? new List<string>();

        static IEnumerable<object> Bools(List<bool> list)
        {
            var l = new List<object>();
            foreach (bool b in list ?? new List<bool>()) l.Add(b);
            return l;
        }

        static List<object> Grades(List<Grade> grades)
        {
            var list = new List<object>();
            foreach (var g in grades ?? new List<Grade>()) list.Add(g.ToString());
            return list;
        }

        static List<object> Flags(List<UneaseFlag> flags)
        {
            var list = new List<object>();
            foreach (var f in flags ?? new List<UneaseFlag>())
                if (f != null)
                    list.Add(new Dictionary<string, object>
                    {
                        ["key"] = f.key ?? "",
                        ["value"] = f.value,
                    });
            return list;
        }

        static List<object> Tools(List<Tool> tools)
        {
            var list = new List<object>();
            foreach (var t in tools ?? new List<Tool>()) list.Add(t.ToString());
            return list;
        }

        static List<object> BoardStates(List<CaseBoardSave> states, bool includeV3Fields)
        {
            var list = new List<object>();
            foreach (var b in states ?? new List<CaseBoardSave>())
            {
                if (b == null) continue;
                var links = new List<object>();
                foreach (var l in b.links ?? new List<BoardLinkSave>())
                    if (l != null)
                        links.Add(new Dictionary<string, object>
                        {
                            ["sourceId"] = l.sourceId ?? "",
                            ["targetId"] = l.targetId ?? "",
                        });
                var positions = new List<object>();
                foreach (var p in b.nodePositions ?? new List<NodePositionSave>())
                    if (p != null)
                        positions.Add(new Dictionary<string, object>
                        {
                            ["clueId"] = p.clueId ?? "",
                            ["x"] = p.x,
                            ["y"] = p.y,
                        });
                var tiers = new List<object>();
                foreach (int t in b.firedHintTiers ?? new List<int>()) tiers.Add(t);
                var board = new Dictionary<string, object>
                {
                    ["caseId"] = b.caseId,
                    ["visibleClueIds"] = new List<object>(Strings(b.visibleClueIds)),
                    ["collectedClueIds"] = new List<object>(Strings(b.collectedClueIds)),
                    ["links"] = links,
                    ["nodePositions"] = positions,
                    ["selectedConclusionId"] = b.selectedConclusionId ?? "",
                    ["caseErrorCount"] = b.caseErrorCount,
                    ["firedHintTiers"] = tiers,
                    ["linkingLockedOut"] = b.linkingLockedOut,
                    ["submittedConclusionId"] = b.submittedConclusionId ?? "",
                    ["boardSolved"] = b.boardSolved,
                    ["completionRewardApplied"] = b.completionRewardApplied,
                };
                if (includeV3Fields)
                {
                    board["submittedWrongConclusionIds"] =
                        new List<object>(Strings(b.submittedWrongConclusionIds));
                    board["submittedPartialConclusionIds"] =
                        new List<object>(Strings(b.submittedPartialConclusionIds));
                }
                list.Add(board);
            }
            return list;
        }

        static List<object> CaseGradeStates(List<CaseGradeSave> states)
        {
            var list = new List<object>();
            foreach (var g in states ?? new List<CaseGradeSave>())
                if (g != null)
                    list.Add(new Dictionary<string, object>
                    {
                        ["caseId"] = g.caseId,
                        ["caseErrorCount"] = g.caseErrorCount,
                        ["submittedWrongConclusionIds"] =
                            new List<object>(Strings(g.submittedWrongConclusionIds)),
                        ["submittedPartialConclusionIds"] =
                            new List<object>(Strings(g.submittedPartialConclusionIds)),
                        ["interrogationStrikeCount"] = g.interrogationStrikeCount,
                        ["mistrialUsed"] = g.mistrialUsed,
                        ["strainedScanUsed"] = g.strainedScanUsed,
                        ["displayGrade"] = g.displayGrade,
                    });
            return list;
        }

        static object PlayerPosition(PlayerPositionSave position)
        {
            if (position == null) return null;
            return new Dictionary<string, object>
            {
                ["x"] = position.x,
                ["y"] = position.y,
                ["z"] = position.z,
            };
        }

        static object ActiveInterrogation(ActiveInterrogationSave state)
        {
            if (state == null) return null;
            return new Dictionary<string, object>
            {
                ["caseId"] = state.caseId,
                ["phaseIndex"] = state.phaseIndex,
                ["composureSegmentsRemaining"] = state.composureSegmentsRemaining,
                ["strikeCount"] = state.strikeCount,
                ["entryStrikeCount"] = state.entryStrikeCount,
                ["preInterrogationCheckpointId"] = state.preInterrogationCheckpointId ?? "",
            };
        }
    }
}
