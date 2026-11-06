using System.Collections.Generic;
using Morsoul.Engine;
using UnityEngine;

namespace Morsoul.Data
{
    /// <summary>
    /// Authored weighted evidence graph for one case (Data_Schemas.md active
    /// schema; replaces the slot/premise board for new content). Runtime
    /// board state lives in the save/session, not here.
    /// </summary>
    [CreateAssetMenu(menuName = "Morsoul/Evidence Graph")]
    public class EvidenceGraphData : ScriptableObject
    {
        public int caseId;
        /// <summary>Drafted numbers awaiting design sign-off; not yet canonical.</summary>
        public bool provisional;
        [TextArea] public string designNote;
        public string[] clueIds = new string[0];
        public ClueReliability[] reliabilities = new ClueReliability[0];
        public WeightedClueEdge[] authoredClueEdges = new WeightedClueEdge[0];
        public DirectedEdge[] solutionEdges = new DirectedEdge[0];
        public DirectedEdge[] unlockDependencies = new DirectedEdge[0];
        public ConclusionDef[] conclusions = new ConclusionDef[0];
        public string startingWrongConclusionId;
        public string completeConclusionId;
        public SolverParameters parameters = new SolverParameters();
        public float cachedReferenceSupport;
        public string[] solutionTopologicalOrder = new string[0];

        public EvidenceGraphDef ToDef() => new EvidenceGraphDef
        {
            CaseId = caseId,
            ClueIds = clueIds ?? new string[0],
            Reliabilities = reliabilities ?? new ClueReliability[0],
            AuthoredClueEdges = authoredClueEdges ?? new WeightedClueEdge[0],
            SolutionEdges = solutionEdges ?? new DirectedEdge[0],
            UnlockDependencies = unlockDependencies ?? new DirectedEdge[0],
            Conclusions = conclusions ?? new ConclusionDef[0],
            StartingWrongConclusionId = startingWrongConclusionId,
            CompleteConclusionId = completeConclusionId,
            Parameters = parameters ?? new SolverParameters(),
            CachedReferenceSupport = cachedReferenceSupport,
            SolutionTopologicalOrder = solutionTopologicalOrder ?? new string[0],
        };

        /// <summary>
        /// Runs the full authoring validation (constraints C1-C7, size
        /// limits, ranges, cached-value freshness). Errors are prefixed with
        /// the asset name for readability in bulk validators.
        /// </summary>
        public List<string> Validate()
        {
            var errors = EvidenceGraph.Build(ToDef(), out _);
            if (errors.Count > 0)
                for (int i = 0; i < errors.Count; i++)
                    errors[i] = $"{name}: {errors[i]}";
            return errors;
        }

        /// <summary>
        /// Builds the immutable runtime graph, or null when validation
        /// fails (errors are then available from Validate()).
        /// </summary>
        public EvidenceGraph BuildGraph()
        {
            EvidenceGraph.Build(ToDef(), out var graph);
            return graph;
        }
    }
}
