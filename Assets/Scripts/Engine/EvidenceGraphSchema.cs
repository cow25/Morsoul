using System;
using System.Collections.Generic;

namespace Morsoul.Engine
{
    // Serializable schema types for the weighted deduction board
    // (Specs/Algorithm.md, Specs/05_Production/Data_Schemas.md).
    // Pure POCOs: no UnityEngine, no I/O. Used both as Unity-serialized
    // fields on EvidenceGraphData and as plain test doubles.

    /// <summary>Directed link between two clue ids. Value type with ordinal id equality.</summary>
    [Serializable]
    public struct DirectedEdge : IEquatable<DirectedEdge>
    {
        public string sourceId;
        public string targetId;

        public DirectedEdge(string sourceId, string targetId)
        {
            this.sourceId = sourceId;
            this.targetId = targetId;
        }

        public bool Equals(DirectedEdge other) =>
            string.Equals(sourceId, other.sourceId, StringComparison.Ordinal) &&
            string.Equals(targetId, other.targetId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is DirectedEdge other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = sourceId != null ? StringComparer.Ordinal.GetHashCode(sourceId) : 0;
                return (h * 397) ^ (targetId != null ? StringComparer.Ordinal.GetHashCode(targetId) : 0);
            }
        }

        public override string ToString() => sourceId + "->" + targetId;
    }

    /// <summary>Weighted clue-to-clue authored edge (E_KK).</summary>
    [Serializable]
    public class WeightedClueEdge
    {
        public string sourceId;
        public string targetId;
        public float weight = 1f;
    }

    /// <summary>Weighted clue-to-conclusion edge (E+ supporting / E- contradicting).</summary>
    [Serializable]
    public class WeightedClueRef
    {
        public string clueId;
        public float weight = 1f;
    }

    /// <summary>Clue reliability r : K -> (0, 1].</summary>
    [Serializable]
    public class ClueReliability
    {
        public string clueId;
        public float value = 1f;
    }

    public enum ConclusionTier
    {
        Wrong = 0,
        Partial = 1,
        Complete = 2,
    }

    /// <summary>One authored conclusion h in H.</summary>
    [Serializable]
    public class ConclusionDef
    {
        public string id;
        public string text;
        public ConclusionTier tier;
        public WeightedClueRef[] supportingEdges = new WeightedClueRef[0];
        public WeightedClueRef[] contradictingEdges = new WeightedClueRef[0];
    }

    /// <summary>Engine parameters theta = (alpha, lambda, gamma, theta_low, theta_high).</summary>
    [Serializable]
    public class SolverParameters
    {
        public float alpha = 0.85f;
        public float contradictionWeight;
        public float inventedLinkPenalty;
        public float lowThreshold;
        public float highThreshold = 1f;

        public SolverParameters Clone() => new SolverParameters
        {
            alpha = alpha,
            contradictionWeight = contradictionWeight,
            inventedLinkPenalty = inventedLinkPenalty,
            lowThreshold = lowThreshold,
            highThreshold = highThreshold,
        };
    }

    /// <summary>
    /// Plain-data definition of a case's evidence graph. Mirrors
    /// EvidenceGraphData's serialized fields so the same validation and
    /// solver code runs on authored assets and synthetic test data.
    /// </summary>
    public class EvidenceGraphDef
    {
        public int CaseId;
        public string[] ClueIds = new string[0];
        public ClueReliability[] Reliabilities = new ClueReliability[0];
        public WeightedClueEdge[] AuthoredClueEdges = new WeightedClueEdge[0];
        public DirectedEdge[] SolutionEdges = new DirectedEdge[0];
        public DirectedEdge[] UnlockDependencies = new DirectedEdge[0];
        public ConclusionDef[] Conclusions = new ConclusionDef[0];
        public string StartingWrongConclusionId;
        public string CompleteConclusionId;
        public SolverParameters Parameters = new SolverParameters();
        public float CachedReferenceSupport;
        public string[] SolutionTopologicalOrder = new string[0];
    }
}
