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

        /// <summary>Case 0 tutorial IDs that trigger the authored red-herring rejection.</summary>
        public string[] tutorialRedHerringClueIds = new string[0];
        [TextArea] public string tutorialRedHerringFeedback;
        [TextArea] public string tutorialReversedLinkFeedback;
        [TextArea] public string tutorialGenericLinkFeedback;
        [TextArea] public string tutorialLockedConclusionFeedback;

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

        /// <summary>Authored submission banner for a conclusion, or null when none exists.</summary>
        public string GetConclusionSubmissionFeedback(string conclusionId)
        {
            if (string.IsNullOrEmpty(conclusionId) || conclusions == null) return null;
            foreach (var conclusion in conclusions)
                if (conclusion != null && conclusion.id == conclusionId)
                    return string.IsNullOrEmpty(conclusion.submissionFeedback)
                        ? null
                        : conclusion.submissionFeedback;
            return null;
        }

        /// <summary>
        /// Case 0 tutorial rejection text for an attempted link, or null when the
        /// graph does not author a specific response.
        /// </summary>
        public string GetTutorialLinkFeedback(DirectedEdge attempted)
        {
            if (!string.IsNullOrEmpty(tutorialRedHerringFeedback) &&
                tutorialRedHerringClueIds != null)
                foreach (string clueId in tutorialRedHerringClueIds)
                    if (clueId == attempted.sourceId || clueId == attempted.targetId)
                        return tutorialRedHerringFeedback;

            if (!string.IsNullOrEmpty(tutorialReversedLinkFeedback))
            {
                var reversed = new DirectedEdge(attempted.targetId, attempted.sourceId);
                if (ContainsEdge(authoredClueEdges, reversed) || ContainsEdge(solutionEdges, reversed))
                    return tutorialReversedLinkFeedback;
            }

            return string.IsNullOrEmpty(tutorialGenericLinkFeedback)
                ? null
                : tutorialGenericLinkFeedback;
        }

        static bool ContainsEdge(WeightedClueEdge[] edges, DirectedEdge edge)
        {
            if (edges == null) return false;
            foreach (var candidate in edges)
                if (candidate != null &&
                    candidate.sourceId == edge.sourceId &&
                    candidate.targetId == edge.targetId)
                    return true;
            return false;
        }

        static bool ContainsEdge(DirectedEdge[] edges, DirectedEdge edge)
        {
            if (edges == null) return false;
            foreach (var candidate in edges)
                if (candidate.Equals(edge))
                    return true;
            return false;
        }
    }
}
