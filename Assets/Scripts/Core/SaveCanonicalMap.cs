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
        public static Dictionary<string, object> ToCanonical(SaveState s)
        {
            var map = new Dictionary<string, object>
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
                    ["strainedScanUsed"] = s.strainedScanUsed,
                },
                ["boardStates"] = BoardStates(s.boardStates),
            };
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

        static List<object> BoardStates(List<CaseBoardSave> states)
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
                list.Add(new Dictionary<string, object>
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
                });
            }
            return list;
        }
    }
}
