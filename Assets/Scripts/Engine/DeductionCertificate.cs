using System;
using System.Collections.Generic;

namespace Morsoul.Engine
{
    public enum DeductionStatus
    {
        Ok = 0,
        InvalidInput = 1,
        ConclusionLocked = 2,
    }

    public enum SupportBand
    {
        Cold = 0,
        Warm = 1,
        Hot = 2,
    }

    /// <summary>
    /// Solver input I = (G, U, C, L, h): the graph plus the player's board
    /// state. All ids are stable clue/conclusion ids; links are directed
    /// (u, v) pairs between collected clues.
    /// </summary>
    public sealed class DeductionInput
    {
        public string[] VisibleClueIds = new string[0];    // U subset of K
        public string[] CollectedClueIds = new string[0];  // C subset of U
        public DirectedEdge[] Links = new DirectedEdge[0]; // L, drawn links
        public string SelectedConclusionId;                // h in H
    }

    /// <summary>
    /// Output certificate O per Algorithm.md 1.2.4 / Data_Schemas.md. Pure
    /// data; produced by DeductionEngine.Solve only. On invalid input only
    /// Status and Errors are meaningful.
    /// </summary>
    public sealed class DeductionCertificate
    {
        public DeductionStatus Status;
        public string SelectedConclusionId;
        public ConclusionTier Verdict;                              // tier of the selected conclusion
        public ConclusionTier ResolvedTier { get; set; }            // the engine's highest unlocked verdict
        public double SelectedSupport;                              // phi(h) in [0, 1]
        public SupportBand Band;
        public IReadOnlyDictionary<string, double> SupportByConclusion; // phi per hypothesis
        public IReadOnlyDictionary<string, double> PropagatedClueValues; // x over K_act
        public IReadOnlyDictionary<string, double> SolutionPropagatedClueValues { get; set; } // solution-only propagation
        public int InventedLinkCount;                               // |L_spur|
        public IReadOnlyList<string> UncollectedClueIds;            // U \ C
        public IReadOnlyList<string> UnlinkedClueIds;               // C \ K_act
        public double RuntimeMs;
        public IReadOnlyList<string> Errors = new List<string>(0);

        public bool IsSolved =>
            Status == DeductionStatus.Ok && Verdict == ConclusionTier.Complete;

        internal static DeductionCertificate Invalid(IReadOnlyList<string> errors,
            double runtimeMs = 0) => new DeductionCertificate
        {
            Status = DeductionStatus.InvalidInput,
            Verdict = ConclusionTier.Wrong,
            ResolvedTier = ConclusionTier.Wrong,
            Band = SupportBand.Cold,
            SupportByConclusion = new Dictionary<string, double>(),
            PropagatedClueValues = new Dictionary<string, double>(),
            SolutionPropagatedClueValues = new Dictionary<string, double>(),
            UncollectedClueIds = new List<string>(0),
            UnlinkedClueIds = new List<string>(0),
            RuntimeMs = runtimeMs,
            Errors = errors,
        };
    }
}
