using System;
using System.Collections.Generic;
using System.Linq;

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
        /// <summary>Focus regained per whole minute of rest.</summary>
        public const float RestEnergyPerMinute = 5f;
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
        public bool BoardSolved;
        public bool CompletionRewardApplied;
        public float Focus = 100f;
        public float MaxFocus = 100f;
    }

    public enum ProgressionEventKind
    {
        /// <summary>Structurally invalid draw (self-link or uncollected endpoint) — never charged.</summary>
        InvalidDraw,
        /// <summary>Linking locked out and Focus below the penalty — draw ignored.</summary>
        Blocked,
        /// <summary>The link is already on the board — duplicate delivery, no charge.</summary>
        LinkDuplicate,
        /// <summary>The link is on the solution path (in S*) — free.</summary>
        LinkOk,
        /// <summary>Off-S* link: error counted, Focus charged (was affordable).</summary>
        LinkWrong,
        /// <summary>Off-S* link the player could not afford (Cases 1-4): board cleared, linking locked.</summary>
        LinksCleared,
        /// <summary>Off-S* link the player could not afford (Case 5): run the Ending Resolver.</summary>
        ResolveEnding,
        /// <summary>An error-tier threshold fired once (Tier = 1..3).</summary>
        HintTierFired,
        /// <summary>Supported complete submission: board solved, capacity bonus applied.</summary>
        SubmissionSolved,
        /// <summary>Wrong/partial (or locked) submission: nothing awarded.</summary>
        SubmissionRejected,
        /// <summary>Complete submission again after the reward was already applied.</summary>
        SubmissionAlreadyRewarded,
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
        /// on_link_drawn: validates the link once. Off-S* links cost 20
        /// Focus when affordable; unaffordable errors clear the board and
        /// lock linking (Cases 1-4) or request the Ending Resolver (Case 5)
        /// before any tier effects. Solution links are free.
        /// </summary>
        public static List<ProgressionEvent> OnLinkDrawn(
            BoardRuntimeState state, EvidenceGraph graph, DirectedEdge link)
        {
            var events = new List<ProgressionEvent>();
            if (state.LinkingLockedOut && state.Focus < ProgressionConstants.ErrorFocusCost)
            {
                events.Add(new ProgressionEvent { Kind = ProgressionEventKind.Blocked });
                return events;
            }

            state.LinkingLockedOut = false;
            if (state.Links.Contains(link))
            {
                // Same link delivered twice (UI re-entry) — never charge twice.
                events.Add(new ProgressionEvent { Kind = ProgressionEventKind.LinkDuplicate });
                return events;
            }
            state.Links.Add(link);

            if (graph != null && graph.IsSolutionLink(link))
            {
                events.Add(new ProgressionEvent { Kind = ProgressionEventKind.LinkOk });
                return events;
            }

            state.CaseErrorCount++;
            if (state.Focus >= ProgressionConstants.ErrorFocusCost)
            {
                state.Focus -= ProgressionConstants.ErrorFocusCost;
            }
            else if (graph != null && graph.CaseId == 5)
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
                else if (graph != null)
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

        /// <summary>on_rest_minute: +5 per whole minute rested, capped at max.</summary>
        public static void OnRestMinutes(BoardRuntimeState state, int wholeMinutes)
        {
            if (wholeMinutes <= 0) return;
            state.Focus = Math.Min(state.MaxFocus,
                state.Focus + ProgressionConstants.RestEnergyPerMinute * wholeMinutes);
        }

        /// <summary>on_reopen: +floor(minutes_closed / 2), capped at max.</summary>
        public static void OnReopen(BoardRuntimeState state, int wholeMinutesClosed)
        {
            if (wholeMinutesClosed <= 0) return;
            state.Focus = Math.Min(state.MaxFocus,
                state.Focus + wholeMinutesClosed / ProgressionConstants.ClosedMinutesPerEnergy);
        }

        /// <summary>
        /// on_submit: a supported complete submission solves the board and,
        /// once per case, raises max Focus by 10 and refills. Wrong/partial
        /// or locked submissions award nothing and never mark the board.
        /// </summary>
        public static List<ProgressionEvent> OnSubmission(
            BoardRuntimeState state, DeductionCertificate certificate)
        {
            var events = new List<ProgressionEvent>();
            if (certificate == null || !certificate.IsSolved)
            {
                events.Add(new ProgressionEvent { Kind = ProgressionEventKind.SubmissionRejected });
                return events;
            }

            state.SubmittedConclusionId = state.SelectedConclusionId;
            state.BoardSolved = true;
            if (state.CompletionRewardApplied)
            {
                events.Add(new ProgressionEvent { Kind = ProgressionEventKind.SubmissionAlreadyRewarded });
                return events;
            }

            state.CompletionRewardApplied = true;
            state.MaxFocus += ProgressionConstants.CompleteSubmissionCapacityBonus;
            state.Focus = state.MaxFocus;
            events.Add(new ProgressionEvent { Kind = ProgressionEventKind.SubmissionSolved });
            return events;
        }
    }
}
