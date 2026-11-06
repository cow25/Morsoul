using System.Collections.Generic;
using System.Linq;
using Morsoul.Engine;
using NUnit.Framework;

namespace Morsoul.Tests
{
    /// <summary>
    /// Algorithm 2 — progression rules on BoardRuntimeState: per-link
    /// validation, Focus 20 boundary, lockout, one-time hint tiers,
    /// rest/reopen recovery and the guarded completion reward.
    /// </summary>
    public class ProgressionRulesTests
    {
        EvidenceGraph _graph;
        BoardRuntimeState _state;

        [SetUp]
        public void SetUp()
        {
            _graph = TestGraphs.BuildValid();
            _state = new BoardRuntimeState
            {
                CaseId = _graph.CaseId,
                VisibleClueIds = _graph.ClueIds.ToList(),
                CollectedClueIds = _graph.ClueIds.ToList(),
                Focus = 100f,
                MaxFocus = 100f,
            };
        }

        static bool HasEvent(List<ProgressionEvent> events, ProgressionEventKind kind) =>
            events.Any(e => e.Kind == kind);

        static List<ProgressionEvent> Draw(BoardRuntimeState s, EvidenceGraph g,
            string a, string b) => ProgressionRules.OnLinkDrawn(s, g, new DirectedEdge(a, b));

        [Test]
        public void SolutionLink_IsFree_NoError()
        {
            var events = Draw(_state, _graph, "a", "b");
            Assert.That(HasEvent(events, ProgressionEventKind.LinkOk), Is.True);
            Assert.That(_state.CaseErrorCount, Is.EqualTo(0));
            Assert.That(_state.Focus, Is.EqualTo(100f));
            Assert.That(_state.Links, Has.Count.EqualTo(1));
        }

        [Test]
        public void AuthoredOffSolutionLink_CountsError_ChargesFocus()
        {
            var events = Draw(_state, _graph, "d", "e"); // authored, but not in S*
            Assert.That(HasEvent(events, ProgressionEventKind.LinkWrong), Is.True);
            Assert.That(_state.CaseErrorCount, Is.EqualTo(1));
            Assert.That(_state.Focus, Is.EqualTo(80f));
        }

        [Test]
        public void InventedLink_IsAlsoAnError()
        {
            Draw(_state, _graph, "c", "a"); // not authored at all
            Assert.That(_state.CaseErrorCount, Is.EqualTo(1));
            Assert.That(_state.Focus, Is.EqualTo(80f));
        }

        [Test]
        public void DuplicateDelivery_DoesNotChargeTwice()
        {
            Draw(_state, _graph, "c", "a");
            var second = Draw(_state, _graph, "c", "a");
            Assert.That(HasEvent(second, ProgressionEventKind.LinkDuplicate), Is.True);
            Assert.That(_state.CaseErrorCount, Is.EqualTo(1));
            Assert.That(_state.Focus, Is.EqualTo(80f));
        }

        [Test]
        public void FocusBoundary_AtTwenty_Charges_ReachesZero_NoLockout()
        {
            _state.Focus = 20f;
            Draw(_state, _graph, "c", "a");
            Assert.That(_state.Focus, Is.EqualTo(0f));
            Assert.That(_state.LinkingLockedOut, Is.False);
            Assert.That(_state.Links, Has.Count.EqualTo(1));
        }

        [Test]
        public void UnaffordableError_ClearsLinks_LocksOut_UntilTwenty()
        {
            _state.Focus = 20f;
            Draw(_state, _graph, "c", "a");   // -> F 0
            var cleared = Draw(_state, _graph, "d", "c"); // unaffordable
            Assert.That(HasEvent(cleared, ProgressionEventKind.LinksCleared), Is.True);
            Assert.That(_state.Links, Is.Empty);
            Assert.That(_state.LinkingLockedOut, Is.True);
            Assert.That(_state.CaseErrorCount, Is.EqualTo(2));

            // Still below 20: draws are blocked and never reach the board.
            var blocked = Draw(_state, _graph, "a", "b");
            Assert.That(HasEvent(blocked, ProgressionEventKind.Blocked), Is.True);
            Assert.That(_state.Links, Is.Empty);

            // Restoring to exactly 20 unlatches on the next draw.
            ProgressionRules.OnRestMinutes(_state, 4); // +20 -> F 20
            var drawn = Draw(_state, _graph, "a", "b");
            Assert.That(HasEvent(drawn, ProgressionEventKind.LinkOk), Is.True);
            Assert.That(_state.LinkingLockedOut, Is.False);
            Assert.That(_state.Links, Has.Count.EqualTo(1));
        }

        [Test]
        public void UnaffordableError_InCase5_ResolvesEnding_InsteadOfClearing()
        {
            var def = TestGraphs.FiveClueDef();
            def.CaseId = 5;
            var g5 = TestGraphs.BuildValid(def);
            _state.CaseId = 5;
            _state.Focus = 10f;

            var events = Draw(_state, g5, "c", "a");
            Assert.That(HasEvent(events, ProgressionEventKind.ResolveEnding), Is.True);
            Assert.That(HasEvent(events, ProgressionEventKind.LinksCleared), Is.False);
            Assert.That(_state.Links, Has.Count.EqualTo(1)); // the erroneous link stays
            Assert.That(_state.LinkingLockedOut, Is.False);
        }

        [Test]
        public void Tier1_AtFiveErrors_GrantsTwentyFocus_Once()
        {
            // 5 errors at 20 each from F 100 -> the 5th is affordable, then +20 tier reward.
            string[] wrong = { "c", "a", "d", "c", "e", "b", "a", "e", "b", "d" };
            var fired = new List<ProgressionEvent>();
            for (int i = 0; i < 5; i++)
                fired.AddRange(Draw(_state, _graph, wrong[i * 2], wrong[i * 2 + 1]));

            Assert.That(_state.CaseErrorCount, Is.EqualTo(5));
            Assert.That(_state.Focus, Is.EqualTo(20f)); // 100 - 100 + 20
            Assert.That(_state.FiredHintTiers, Is.EqualTo(new[] { 1 }));
            Assert.That(fired.Count(e => e.Kind == ProgressionEventKind.HintTierFired && e.Tier == 1),
                Is.EqualTo(1));
        }

        [Test]
        public void Tier2_AtTenErrors_HintsNextUnfinishedSolutionClue()
        {
            _state.Links.Add(new DirectedEdge("a", "b")); // one solution link already drawn
            _state.CaseErrorCount = 9;
            var events = Draw(_state, _graph, "c", "a");

            var tier = events.FirstOrDefault(e => e.Kind == ProgressionEventKind.HintTierFired);
            Assert.That(tier, Is.Not.Null);
            Assert.That(tier.Tier, Is.EqualTo(2));
            Assert.That(tier.HintClueId, Is.EqualTo("b")); // next(L): b->c still missing
            Assert.That(tier.HintClueCollected, Is.True);
        }

        [Test]
        public void Tier2_UncollectedHintTarget_ReportsMissing()
        {
            _state.CaseErrorCount = 9;
            _state.CollectedClueIds.Remove("a");
            var events = Draw(_state, _graph, "c", "d");
            var tier = events.First(e => e.Kind == ProgressionEventKind.HintTierFired);
            Assert.That(tier.Tier, Is.EqualTo(2));
            Assert.That(tier.HintClueId, Is.EqualTo("a"));
            Assert.That(tier.HintClueCollected, Is.False); // UI shows "something's missing"
        }

        [Test]
        public void Tier3_AtTwentyErrors_HintsFinalSolutionClue()
        {
            _state.CaseErrorCount = 19;
            _state.Focus = 1000f; // stay affordable through the count
            _state.MaxFocus = 1000f;
            var events = Draw(_state, _graph, "c", "a");
            var tier = events.First(e => e.Kind == ProgressionEventKind.HintTierFired);
            Assert.That(tier.Tier, Is.EqualTo(3));
            Assert.That(tier.HintClueId, Is.EqualTo("c")); // last in pi
        }

        [Test]
        public void Tiers_FireExactlyOnce_AcrossRestoredState()
        {
            // Simulate reaching 5 errors, then a save/reload that restores the
            // counters into a fresh state object.
            _state.CaseErrorCount = 5;
            _state.FiredHintTiers.Add(1);
            var restored = new BoardRuntimeState
            {
                CaseId = _state.CaseId,
                CollectedClueIds = _state.CollectedClueIds,
                CaseErrorCount = _state.CaseErrorCount,
                FiredHintTiers = new List<int>(_state.FiredHintTiers),
                Focus = _state.Focus,
                MaxFocus = _state.MaxFocus,
            };
            float before = restored.Focus;
            var events = Draw(restored, _graph, "c", "a"); // error 6 — no tier fires
            Assert.That(restored.CaseErrorCount, Is.EqualTo(6));
            Assert.That(events.Any(e => e.Kind == ProgressionEventKind.HintTierFired), Is.False);
            Assert.That(restored.Focus, Is.EqualTo(before - 20f));
        }

        [Test]
        public void UnaffordableError_SkipsTierEffects_EarlyReturn()
        {
            // Even at a threshold count, the unaffordable branch returns before tiers.
            _state.CaseErrorCount = 4;
            _state.Focus = 10f;
            var events = Draw(_state, _graph, "c", "a"); // error 5, but unaffordable
            Assert.That(_state.CaseErrorCount, Is.EqualTo(5));
            Assert.That(HasEvent(events, ProgressionEventKind.LinksCleared), Is.True);
            Assert.That(events.Any(e => e.Kind == ProgressionEventKind.HintTierFired), Is.False);
            Assert.That(_state.FiredHintTiers, Is.Empty);
        }

        [Test]
        public void RestAndReopen_ApplySpecAmounts()
        {
            _state.Focus = 10f;
            ProgressionRules.OnRestMinutes(_state, 3); // +15
            Assert.That(_state.Focus, Is.EqualTo(25f));
            ProgressionRules.OnReopen(_state, 7); // floor(7/2) = +3
            Assert.That(_state.Focus, Is.EqualTo(28f));
            _state.Focus = 95f;
            ProgressionRules.OnReopen(_state, 100); // would +50, capped at 100
            Assert.That(_state.Focus, Is.EqualTo(100f));
        }

        [Test]
        public void Submission_Complete_Solves_AwardsCapacity_Once()
        {
            var cert = DeductionEngine.Solve(_graph,
                TestGraphs.Input(_graph,
                    new[] { new DirectedEdge("a", "b"), new DirectedEdge("b", "c") },
                    "h_complete"));

            _state.SelectedConclusionId = "h_complete";
            var first = ProgressionRules.OnSubmission(_state, cert);
            Assert.That(HasEvent(first, ProgressionEventKind.SubmissionSolved), Is.True);
            Assert.That(_state.BoardSolved, Is.True);
            Assert.That(_state.MaxFocus, Is.EqualTo(110f));
            Assert.That(_state.Focus, Is.EqualTo(110f));
            Assert.That(_state.SubmittedConclusionId, Is.EqualTo("h_complete"));

            var second = ProgressionRules.OnSubmission(_state, cert);
            Assert.That(HasEvent(second, ProgressionEventKind.SubmissionAlreadyRewarded), Is.True);
            Assert.That(_state.MaxFocus, Is.EqualTo(110f)); // no double award
        }

        [Test]
        public void Submission_PartialOrLocked_Rejected_NoReward()
        {
            var partial = DeductionEngine.Solve(_graph,
                TestGraphs.Input(_graph, new[] { new DirectedEdge("a", "b") }, "h_partial"));
            _state.SelectedConclusionId = "h_partial";
            var events = ProgressionRules.OnSubmission(_state, partial);
            Assert.That(HasEvent(events, ProgressionEventKind.SubmissionRejected), Is.True);
            Assert.That(_state.BoardSolved, Is.False);
            Assert.That(_state.MaxFocus, Is.EqualTo(100f));

            var locked = DeductionEngine.Solve(_graph,
                TestGraphs.Input(_graph, new DirectedEdge[0], "h_complete"));
            Assert.That(locked.Status, Is.EqualTo(DeductionStatus.ConclusionLocked));
            Assert.That(HasEvent(ProgressionRules.OnSubmission(_state, locked),
                ProgressionEventKind.SubmissionRejected), Is.True);
        }
    }
}
