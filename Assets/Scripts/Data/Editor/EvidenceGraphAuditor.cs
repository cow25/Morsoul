using Morsoul.Engine;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Data.Editor
{
    /// <summary>
    /// Design-time audit for EvidenceGraphData assets (Algorithm.md User
    /// Story 2 + constraints C1-C7). For every graph asset: validates the
    /// definition, then enumerates all 2^|E_KK| authored-link subsets through
    /// the solver and reports which boards unlock each conclusion. Intended
    /// check: exactly one board unlocks the complete conclusion, and that
    /// board contains S*. Run after editing graph data.
    /// </summary>
    public static class EvidenceGraphAuditor
    {
        [MenuItem("Morsoul/Data/Audit Evidence Graphs")]
        public static void AuditAll()
        {
            var guids = AssetDatabase.FindAssets("t:EvidenceGraphData");
            if (guids.Length == 0)
            {
                Debug.Log("EvidenceGraphAuditor: no EvidenceGraphData assets found.");
                return;
            }

            int errors = 0, warnings = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<EvidenceGraphData>(path);
                if (data == null) continue;

                var validation = data.Validate();
                foreach (string error in validation)
                {
                    Debug.LogError($"EvidenceGraphAuditor: {error}", data);
                    errors++;
                }
                if (validation.Count > 0) continue; // cannot audit an invalid graph

                var report = GraphAudit.Analyze(data.BuildGraph());
                foreach (string finding in report.Findings)
                {
                    if (finding.StartsWith("ERROR"))
                    {
                        Debug.LogError($"EvidenceGraphAuditor [{data.name}]: {finding}", data);
                        errors++;
                    }
                    else
                    {
                        Debug.LogWarning($"EvidenceGraphAuditor [{data.name}]: {finding}", data);
                        warnings++;
                    }
                }

                var summary = new System.Text.StringBuilder(
                    $"EvidenceGraphAuditor [{data.name}]: {report.EvaluatedSubsets} subsets " +
                    $"over {report.AuthoredLinkCount} authored links");
                foreach (var c in report.Conclusions)
                    if (!c.IsStarting)
                        summary.Append(
                            $"\n  {c.ConclusionId} ({c.Tier}): {c.UnlockingSubsetCount} unlocking, " +
                            $"{c.MinimalUnlockingSets.Count} minimal set(s)");
                Debug.Log(summary.ToString(), data);
            }

            Debug.Log($"EvidenceGraphAuditor: {guids.Length} graph(s), {errors} error(s), {warnings} warning(s).");
        }
    }
}
