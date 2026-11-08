using System;
using System.Collections.Generic;

namespace Morsoul.Engine
{
    /// <summary>
    /// Progression tuning constants (Algorithm.md B2 / Data_Schemas
    /// ProgressionConstants). Engine-fixed values; scanTickSeconds stays
    /// engine-tunable and lives in the runtime layer.
    /// </summary>
    public static class ProgressionConstants
    {
        /// <summary>Focus lost per erroneous link (Pwrong).</summary>
        public const float ErrorFocusCost = 20f;
        /// <summary>Focus regained per completed Focus rest tick.</summary>
        public const float RestEnergyPerTick = 1f;
        /// <summary>Whole closed minutes required per recovered Focus point.</summary>
        public const int ClosedMinutesPerEnergy = 2;
        /// <summary>Case error counts at which hint tiers fire, once each.</summary>
        public static readonly int[] ErrorTierThresholds = { 5, 10, 20 };
        /// <summary>Tier 1 (5 errors): one-time Focus grant, capped at max.</summary>
        public const float TierOneEnergyReward = 20f;
        /// <summary>Supported complete submission: capacity bonus + full refill.</summary>
        public const float CompleteSubmissionCapacityBonus = 10f;
        /// <summary>Focus required before linking is allowed (same as the error cost).</summary>
        public const float LinkingUnlockFocus = ErrorFocusCost;
    }

    /// <summary>Serializable board node position (engine-side; no Unity types).</summary>
    [Serializable]
    public struct NodePosition
    {
        public float x;
        public float y;
    }

    /// <summary>
    /// Per-case mutable board state (Data_Schemas BoardRuntimeState) plus the
    /// mirrored Focus fields the rules evaluate. Pure data; persisted by the
    /// save layer, mutated only through ProgressionRules.
    /// </summary>
    [Serializable]
    public class BoardRuntimeState
    {
        public int CaseId;
        public List<string> VisibleClueIds = new List<string>();
        public List<string> CollectedClueIds = new List<string>();
        public List<DirectedEdge> Links = new List<DirectedEdge>();
        [NonSerialized] // serialized via NodePosition DTOs in the save layer
        public Dictionary<string, NodePosition> NodePositions = new Dictionary<string, NodePosition>();
        public string SelectedConclusionId;
        public int CaseErrorCount;
        public List<int> FiredHintTiers = new List<int>();
        public bool LinkingLockedOut;
        public string SubmittedConclusionId;
        public List<string> SubmittedWrongConclusionIds = new List<string>();
        public List<string> SubmittedPartialConclusionIds = new List<string>();
        public bool BoardSolved;
        public bool CompletionRewardApplied;
        public float Focus = 100f;
        public float MaxFocus = 100f;
    }

    /// <summary>ProgressionEvent.type values from the approved schema.</summary>
    public enum ProgressionEventKind
    {
        /// <summary>Malformed IDs, self-link, unknown clue or uncollected endpoint.</summary>
        InvalidInput,
        /// <summary>Case 0 off-S* draw rejected before joining the board.</summary>
        TutorialLinkRejected,
        /// <summary>The link is already on the board — no error or charge.</summary>
        DuplicateLink,
        /// <summary>The link is on the solution path (in S*) — free.</summary>
        LinkOk,
        /// <summary>Off-S* link: error counted, Focus charged (was affordable).</summary>
        LinkWrong,
        /// <summary>Off-S* link the player could not afford (Cases 1-4): board cleared, linking locked.</summary>
        LinksCleared,
        /// <summary>Linking locked out and Focus below the penalty — draw ignored.</summary>
        Blocked,
        /// <summary>Submission after boardSolved: strict no-op.</summary>
        BoardAlreadySolved,
        /// <summary>Selected conclusion is locked by support or the structural S* gate.</summary>
        ConclusionLocked,
        /// <summary>Unlocked wrong conclusion: records history and affects Clean eligibility.</summary>
        WrongConclusionRejected,
        /// <summary>Unlocked partial conclusion: records history without grade impact.</summary>
        PartialConclusionFeedback,
        /// <summary>Supported complete submission: board solved and case reward applied.</summary>
        CaseComplete,
        /// <summary>Off-S* link the player could not afford (Case 5): run the Ending Resolver.</summary>
        ResolveEnding,
        /// <summary>An error-tier threshold fired once (Tier = 1..3).</summary>
        HintTierFired,
    }

    public sealed class ProgressionEvent
    {
        public ProgressionEventKind Kind;
        public int Tier;
        /// <summary>Tier 2/3 hint target (null when no clue qualifies).</summary>
        public string HintClueId;
        /// <summary>
        /// False when the hint target is not collected — the UI shows the
        /// "something's missing" dialogue instead of a highlight.
        /// </summary>
        public bool HintClueCollected;
        /// <summary>Submitted conclusion for conclusion-history events.</summary>
        public string ConclusionId;
    }

    /// <summary>
    /// Algorithm 2 — PROGRESSION, as pure state rules. Every function takes
    /// the mutable BoardRuntimeState and returns the events raised; nothing
    /// here performs I/O or touches Unity. Callers mirror Focus/MaxFocus
    /// into the state's Focus fields before and after each call.
    /// </summary>
    public static class ProgressionRules
    {
        /// <summary>
        /// on_link_drawn: validate IDs, collapse duplicate delivery, apply the
        /// Case 0 tutorial reject branch, then charge Cases 1-5 progression
        /// errors. Affordability is captured before the Focus deduction.
        /// </summary>
        public static List<ProgressionEvent> OnLinkDrawn(
            BoardRuntimeState state, EvidenceGraph graph, DirectedEdge link)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var events = new List<ProgressionEvent>();

            if (graph == null || link.sourceId == link.targetId ||
                !graph.ContainsClue(link.sourceId) ||
                !graph.ContainsClue(link.targetId) ||
                !state.CollectedClueIds.Contains(link.sourceId) ||
                !state.CollectedClueIds.Contains(link.targetId))
            {
                events.Add(new ProgressionEvent { Kind = ProgressionEventKind.InvalidInput });
                return events;
            }

            if (state.Links.Contains(link))
            {
                events.Add(new ProgressionEvent { Kind = ProgressionEventKind.DuplicateLink });
                return events;
            }

            if (graph.CaseId == 0 && !graph.IsSolutionLink(link))
            {
                events.Add(new ProgressionEvent { Kind = ProgressionEventKind.TutorialLinkRejected });
                return events;
            }

            if (graph.CaseId != 0 && state.LinkingLockedOut &&
                state.Focus < ProgressionConstants.ErrorFocusCost)
            {
                events.Add(new ProgressionEvent { Kind = ProgressionEventKind.Blocked });
                return events;
            }
            state.LinkingLockedOut = false;
            state.Links.Add(link);

            if (graph.IsSolutionLink(link))
            {
                events.Add(new ProgressionEvent { Kind = ProgressionEventKind.LinkOk });
                return events;
            }

            state.CaseErrorCount++;
            if (state.Focus >= ProgressionConstants.ErrorFocusCost)
            {
                state.Focus -= ProgressionConstants.ErrorFocusCost;
            }
            else if (graph.CaseId == 5)
            {
                events.Add(new ProgressionEvent { Kind = ProgressionEventKind.ResolveEnding });
                return events;
            }
            else
            {
                state.Links.Clear();
                state.LinkingLockedOut = true;
                events.Add(new ProgressionEvent { Kind = ProgressionEventKind.LinksCleared });
                return events;
            }

            events.Add(new ProgressionEvent { Kind = ProgressionEventKind.LinkWrong });
            for (int tier = 0; tier < ProgressionConstants.ErrorTierThresholds.Length; tier++)
            {
                int limit = ProgressionConstants.ErrorTierThresholds[tier];
                int tierId = tier + 1;
                if (state.CaseErrorCount != limit || state.FiredHintTiers.Contains(tierId))
                    continue;
                state.FiredHintTiers.Add(tierId);
                var tierEvent = new ProgressionEvent
                {
                    Kind = ProgressionEventKind.HintTierFired,
                    Tier = tierId,
                };
                if (tierId == 1)
                {
                    state.Focus = Math.Min(state.MaxFocus,
                        state.Focus + ProgressionConstants.TierOneEnergyReward);
                }
                else
                {
                    tierEvent.HintClueId = tierId == 2
                        ? graph.NextUnfinishedSolutionClue(state.Links)
                        : graph.FinalSolutionClue;
                    tierEvent.HintClueCollected = tierEvent.HintClueId != null &&
                        state.CollectedClueIds.Contains(tierEvent.HintClueId);
                }
                events.Add(tierEvent);
            }
            return events;
        }

        /// <summary>on_rest_tick: +1 per completed rest tick, capped at max.</summary>
        public static void OnRestTicks(BoardRuntimeState state, int completedTicks)
        {
            if (state == null || completedTicks <= 0) return;
            state.Focus = Math.Min(state.MaxFocus,
                state.Focus + ProgressionConstants.RestEnergyPerTick * completedTicks);
        }

        /// <summary>on_reopen: +floor(minutes_closed / 2), capped at max.</summary>
        public static void OnReopen(BoardRuntimeState state, int wholeMinutesClosed)
        {
            if (state == null || wholeMinutesClosed <= 0) return;
            state.Focus = Math.Min(state.MaxFocus,
                state.Focus + wholeMinutesClosed / ProgressionConstants.ClosedMinutesPerEnergy);
        }

        /// <summary>
        /// on_submit: locked conclusions no-op; wrong and partial submissions
        /// update history only; supported complete submissions solve the board
        /// and apply the case-specific reward once. Solved boards reject every
        /// later submission before any state change.
        /// </summary>
        public static List<ProgressionEvent> OnSubmission(
            BoardRuntimeState state, DeductionCertificate certificate)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var events = new List<ProgressionEvent>();

            if (state.BoardSolved)
            {
                events.Add(new ProgressionEvent { Kind = ProgressionEventKind.BoardAlreadySolved });
                return events;
            }

            if (certificate == null || certificate.Status == DeductionStatus.InvalidInput)
            {
                events.Add(new ProgressionEvent { Kind = ProgressionEventKind.InvalidInput });
                return events;
            }

            string conclusionId = certificate.SelectedConclusionId ?? state.SelectedConclusionId;
            if (certificate.Status == DeductionStatus.ConclusionLocked)
            {
                events.Add(new ProgressionEvent
                {
                    Kind = ProgressionEventKind.ConclusionLocked,
                    ConclusionId = conclusionId,
                });
                return events;
            }

            state.SubmittedConclusionId = conclusionId;
            switch (certificate.Verdict)
            {
                case ConclusionTier.Wrong:
                    if (!string.IsNullOrEmpty(conclusionId) &&
                        !state.SubmittedWrongConclusionIds.Contains(conclusionId))
                        state.SubmittedWrongConclusionIds.Add(conclusionId);
                    events.Add(new ProgressionEvent
                    {
                        Kind = ProgressionEventKind.WrongConclusionRejected,
                        ConclusionId = conclusionId,
                    });
                    break;

                case ConclusionTier.Partial:
                    if (!string.IsNullOrEmpty(conclusionId) &&
                        !state.SubmittedPartialConclusionIds.Contains(conclusionId))
                        state.SubmittedPartialConclusionIds.Add(conclusionId);
                    events.Add(new ProgressionEvent
                    {
                        Kind = ProgressionEventKind.PartialConclusionFeedback,
                        ConclusionId = conclusionId,
                    });
                    break;

                case ConclusionTier.Complete:
                    state.BoardSolved = true;
                    if (state.CaseId >= 1 && state.CaseId <= 4 &&
                        !state.CompletionRewardApplied)
                    {
                        state.CompletionRewardApplied = true;
                        state.MaxFocus += ProgressionConstants.CompleteSubmissionCapacityBonus;
                        state.Focus = state.MaxFocus;
                    }
                    else if (state.CaseId == 0)
                    {
                        state.Focus = state.MaxFocus;
                    }
                    events.Add(new ProgressionEvent
                    {
                        Kind = ProgressionEventKind.CaseComplete,
                        ConclusionId = conclusionId,
                    });
                    break;
            }
            return events;
        }
    }
}
