using System.Collections.Generic;
using Morsoul.Engine;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Data.Editor
{
    /// <summary>
    /// Drafts provisional EvidenceGraphData for Case 0 (tutorial) and Case 1
    /// (vault) so the weighted board can run and be reviewed before final
    /// design sign-off. Numbers are mapped from the case specs' preserved
    /// reasoning chains; every asset is flagged provisional and audited on
    /// generation: each graph must build cleanly and yield exactly one board
    /// (S*) unlocking the complete conclusion. Re-running overwrites the
    /// provisional assets in place (stable GUIDs) — do not hand-edit them;
    /// tune here instead.
    /// </summary>
    public static class ProvisionalEvidenceGraphGenerator
    {
        const string Note0 =
            "PROVISIONAL pending design approval. S*: C1->C2 (overload = unpowered " +
            "lockdown), C3->T1 (key calibrated by working sensors), C2->T1 " +
            "(manual port is the only path). Support on T1 (calibration) only; " +
            "RH1 pipe contradicts. Tutorial topology: every non-S* link touches " +
            "the red herring.";
        const string Note1 =
            "PROVISIONAL pending design approval. Serial S*: C1->T1 (pristine " +
            "lock + registry = inside access), T1->C2 (access claim reframes " +
            "residue as staged), C2->C3 (faked attack + ledger = embezzlement). " +
            "Support on T1+C3; T2 (Ignatius' cabal claim) and RH1 (planted " +
            "lockpick) contradict. Any stray authored link breaks the unlock - " +
            "the dissonance mechanic.";

        [MenuItem("Morsoul/Data/Draft Provisional Evidence Graphs")]
        public static void Draft()
        {
            int written = 0;
            written += Write(Case0(), "Assets/Data/Cases/Case00/EVG_00_PROVISIONAL.asset", Note0);
            written += Write(Case1(), "Assets/Data/Cases/Case01/EVG_01_PROVISIONAL.asset", Note1);
            AssetDatabase.SaveAssets();
            Debug.Log($"ProvisionalEvidenceGraphGenerator: wrote {written} graph asset(s). " +
                      "Audit them via Morsoul > Data > Audit Evidence Graphs.");
        }

        static int Write(EvidenceGraphDef def, string path, string note)
        {
            var errors = EvidenceGraph.Build(def, out var graph);
            if (graph == null)
            {
                Debug.LogError($"ProvisionalEvidenceGraphGenerator: {path} failed validation:\n" +
                               string.Join("\n", errors));
                return 0;
            }
            foreach (string e in errors) Debug.LogWarning($"{path}: {e}");

            var data = AssetDatabase.LoadAssetAtPath<EvidenceGraphData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EvidenceGraphData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.caseId = def.CaseId;
            data.provisional = true;
            data.designNote = note;
            data.clueIds = def.ClueIds;
            data.reliabilities = def.Reliabilities;
            data.authoredClueEdges = def.AuthoredClueEdges;
            data.solutionEdges = def.SolutionEdges;
            data.unlockDependencies = def.UnlockDependencies;
            data.conclusions = def.Conclusions;
            data.startingWrongConclusionId = def.StartingWrongConclusionId;
            data.completeConclusionId = def.CompleteConclusionId;
            data.parameters = def.Parameters;
            data.cachedReferenceSupport = (float)graph.ReferenceSupport;
            data.solutionTopologicalOrder = new List<string>(graph.SolutionOrder).ToArray();

            EditorUtility.SetDirty(data);

            var caseAsset = AssetDatabase.LoadAssetAtPath<CaseData>(
                $"Assets/Data/Cases/Case{def.CaseId:00}/Case_{def.CaseId:00}.asset");
            if (caseAsset != null && caseAsset.evidenceGraph != data)
            {
                caseAsset.evidenceGraph = data;
                EditorUtility.SetDirty(caseAsset);
            }
            return 1;
        }

        static DirectedEdge E(string s, string t) => new DirectedEdge { sourceId = s, targetId = t };
        static WeightedClueEdge W(string s, string t, float w) =>
            new WeightedClueEdge { sourceId = s, targetId = t, weight = w };
        static ClueReliability R(string id, float v) =>
            new ClueReliability { clueId = id, value = v };
        static WeightedClueRef S(string id, float w) =>
            new WeightedClueRef { clueId = id, weight = w };

        // Tutorial: overload lockdown + working sensors + key = manual override.
        static EvidenceGraphDef Case0()
        {
            var solution = new[] { E("C1_00", "C2_00"), E("C2_00", "T1_00"), E("C3_00", "T1_00") };
            return new EvidenceGraphDef
            {
                CaseId = 0,
                ClueIds = new[] { "C1_00", "C2_00", "C3_00", "T1_00", "RH1_00" },
                Reliabilities = new[]
                {
                    R("C1_00", 1f), R("C2_00", 1f), R("C3_00", 1f),
                    R("T1_00", 0.85f), R("RH1_00", 0.7f),
                },
                AuthoredClueEdges = new[]
                {
                    W("C1_00", "C2_00", 1f),    // S*
                    W("C2_00", "T1_00", 0.5f),  // S*
                    W("C3_00", "T1_00", 0.5f),  // S*
                    W("RH1_00", "C2_00", 0.5f), // pipe cannot move a stone lock
                    W("RH1_00", "C3_00", 0.4f), // pipe cannot break out the key
                    W("RH1_00", "T1_00", 0.1f), // diagnostics rate it useless
                },
                SolutionEdges = solution,
                UnlockDependencies = solution,
                Conclusions = new[]
                {
                    new ConclusionDef
                    {
                        id = "H0_00", tier = ConclusionTier.Wrong,
                        text = "Force the sealed blast door open.",
                        supportingEdges = new[] { S("RH1_00", 0.5f), S("C2_00", 0.2f) },
                    },
                    new ConclusionDef
                    {
                        id = "H1_00", tier = ConclusionTier.Partial,
                        text = "The door is unpowered; something else must open it.",
                        supportingEdges = new[] { S("C1_00", 0.3f), S("C2_00", 0.4f) },
                    },
                    new ConclusionDef
                    {
                        id = "H2_00", tier = ConclusionTier.Complete,
                        text = "Manual Override Escape: calibrate the ancient key " +
                               "into the manual port.",
                        supportingEdges = new[] { S("T1_00", 1f) },
                        contradictingEdges = new[] { S("RH1_00", 1f) },
                    },
                },
                StartingWrongConclusionId = "H0_00",
                CompleteConclusionId = "H2_00",
                Parameters = new SolverParameters
                {
                    alpha = 0.85f, contradictionWeight = 2f,
                    inventedLinkPenalty = 0.15f,
                    lowThreshold = 0.35f, highThreshold = 0.8f,
                },
            };
        }

        // Vault: inside access -> staged attack -> embezzlement ledger.
        static EvidenceGraphDef Case1()
        {
            var solution = new[] { E("C1_01", "T1_01"), E("T1_01", "C2_01"), E("C2_01", "C3_01") };
            return new EvidenceGraphDef
            {
                CaseId = 1,
                ClueIds = new[] { "C1_01", "C2_01", "C3_01", "T1_01", "T2_01", "RH1_01" },
                Reliabilities = new[]
                {
                    R("C1_01", 1f), R("C2_01", 1f), R("C3_01", 1f),
                    R("T1_01", 0.8f), R("T2_01", 0.8f), R("RH1_01", 0.7f),
                },
                AuthoredClueEdges = new[]
                {
                    W("C1_01", "T1_01", 0.5f),  // S*: pristine lock -> only keys
                    W("T1_01", "C2_01", 0.5f),  // S*: inside access -> staged scene
                    W("C2_01", "C3_01", 0.7f),  // S*: fake attack -> cooked ledger
                    W("T2_01", "RH1_01", 0.3f), // cabal claim points at the pick
                    W("T2_01", "C1_01", 0.3f),  // claim tries to explain the lock
                    W("T2_01", "C2_01", 0.4f),  // claim covers the residue
                    W("RH1_01", "C1_01", 0.6f), // the trap: "the pick forced it"
                },
                SolutionEdges = solution,
                UnlockDependencies = solution,
                Conclusions = new[]
                {
                    new ConclusionDef
                    {
                        id = "H0_01", tier = ConclusionTier.Wrong,
                        text = "A cabal breached the vault with volatile dark magic.",
                        supportingEdges = new[] { S("T2_01", 0.5f), S("RH1_01", 0.3f) },
                        contradictingEdges = new[] { S("C1_01", 0.3f) },
                    },
                    new ConclusionDef
                    {
                        id = "H1_01", tier = ConclusionTier.Partial,
                        text = "Someone staged a sabotage, but the culprit is unproven.",
                        supportingEdges = new[]
                        {
                            S("C2_01", 0.4f), S("T2_01", 0.3f), S("RH1_01", 0.2f),
                        },
                    },
                    new ConclusionDef
                    {
                        id = "H2_01", tier = ConclusionTier.Complete,
                        text = "Staged Internal Fraud: Ignatius faked the breach " +
                               "to hide embezzlement.",
                        supportingEdges = new[] { S("T1_01", 0.4f), S("C3_01", 0.6f) },
                        contradictingEdges = new[] { S("RH1_01", 1f), S("T2_01", 0.8f) },
                    },
                },
                StartingWrongConclusionId = "H0_01",
                CompleteConclusionId = "H2_01",
                Parameters = new SolverParameters
                {
                    alpha = 0.85f, contradictionWeight = 2f,
                    inventedLinkPenalty = 0.15f,
                    lowThreshold = 0.25f, highThreshold = 0.8f,
                },
            };
        }
    }
}
