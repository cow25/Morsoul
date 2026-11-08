using System.Collections.Generic;
using System.Linq;
using Morsoul.Board;
using Morsoul.Engine;
using NUnit.Framework;

namespace Morsoul.Tests
{
    /// <summary>
    /// WeightedBoardSession — the headless model behind the new board UI:
    /// clue gating, directed-link draw/sever, conclusion unlock gating,
    /// submission, hint targets and the Focus mirror.
    /// </summary>
    public class WeightedBoardSessionTests
    {
        EvidenceGraph _graph;
        BoardRuntimeState _state;
        WeightedBoardSession _session;

        [SetUp]
        public void SetUp()
        {
            _graph = TestGraphs.BuildValid();
            _state = new BoardRuntimeState
            {
                CaseId = _graph.CaseId,
                Focus = 100f,
                MaxFocus = 100f,
            };
            _session = new WeightedBoardSession(_graph, _state);
        }

        void CollectAll()
        {
            _session.SetVisibleClues(_graph.ClueIds);
            foreach (var id in _graph.ClueIds) _session.CollectClue(id);
        }

        static bool HasEvent(List<ProgressionEvent> events, ProgressionEventKind kind) =>
            events.Any(e => e.Kind == kind);

        [Test]
        public void CollectClue_UnknownId_Rejected()
        {
            Assert.IsFalse(_session.CollectClue("zzz"));
            Assert.IsTrue(_session.CollectClue("a"));
            CollectionAssert.AreEqual(new[] { "a" }, _state.CollectedClueIds);
        }

        [Test]
        public void SetVisibleClues_PrunesCollectedOutsideVisible()
        {
            CollectAll();
            _session.SetVisibleClues(new[] { "a", "b" });
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, _state.VisibleClueIds);
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, _state.CollectedClueIds);
        }

        [Test]
        public void DrawLink_SelfLink_InvalidDraw_NoCharge()
        {
            CollectAll();
            var events = _session.DrawLink(new DirectedEdge("a", "a"));
            Assert.That(HasEvent(events, ProgressionEventKind.InvalidInput), Is.True);
            Assert.That(_state.Links, Is.Empty);
            Assert.That(_state.CaseErrorCount, Is.EqualTo(0));
            Assert.That(_state.Focus, Is.EqualTo(100f));
        }

        [Test]
        public void DrawLink_UncollectedEndpoint_InvalidDraw()
        {
            _session.SetVisibleClues(new[] { "a" });
            _session.CollectClue("a"); // "b" visible-in-graph but not collected
            var events = _session.DrawLink(new DirectedEdge("a", "b"));
            Assert.That(HasEvent(events, ProgressionEventKind.InvalidInput), Is.True);
            Assert.That(_state.Links, Is.Empty);
        }

        [Test]
        public void DrawLink_SolutionLink_Free()
        {
            CollectAll();
            var events = _session.DrawLink(new DirectedEdge("a", "b"));
            Assert.That(HasEvent(events, ProgressionEventKind.LinkOk), Is.True);
            Assert.That(_state.Links, Has.Count.EqualTo(1));
            Assert.That(_state.Focus, Is.EqualTo(100f));
        }

        [Test]
        public void DrawLink_WrongLink_ChargesFocus()
        {
            CollectAll();
            var events = _session.DrawLink(new DirectedEdge("d", "e")); // authored, off-S*
            Assert.That(HasEvent(events, ProgressionEventKind.LinkWrong), Is.True);
            Assert.That(_state.Focus, Is.EqualTo(80f));
        }

        [Test]
        public void SeverLink_FreeRemoval_RedrawIsNewEvent()
        {
            CollectAll();
            _session.DrawLink(new DirectedEdge("d", "e")); // error, -20
            Assert.IsTrue(_session.SeverLink(new DirectedEdge("d", "e")));
            Assert.That(_state.Links, Is.Empty);
            Assert.IsFalse(_session.SeverLink(new DirectedEdge("d", "e")));

            // Re-drawing after a sever is a new draw event — charged again.
            var events = _session.DrawLink(new DirectedEdge("d", "e"));
            Assert.That(HasEvent(events, ProgressionEventKind.LinkWrong), Is.True);
            Assert.That(_state.Focus, Is.EqualTo(60f));
            Assert.That(_state.CaseErrorCount, Is.EqualTo(2));
        }

        [Test]
        public void SelectedConclusion_DefaultsToStarting()
        {
            Assert.That(_session.SelectedConclusionId, Is.EqualTo("h_wrong"));
            _state.SelectedConclusionId = "nonexistent";
            Assert.That(_session.SelectedConclusionId, Is.EqualTo("h_wrong"));
        }

        [Test]
        public void ConclusionUnlock_LockedUntilSupport()
        {
            CollectAll();
            _session.Solve();
            // Empty board: only h0 unlocked; locked conclusions can't be selected.
            Assert.IsFalse(_session.SelectConclusion("h_complete"));
            Assert.That(_session.SelectedConclusionId, Is.EqualTo("h_wrong"));

            // Draw the full solution: S* = {a->b, b->c}; h_complete needs c's support.
            _session.DrawLink(new DirectedEdge("a", "b"));
            _session.DrawLink(new DirectedEdge("b", "c"));
            var unlocked = _session.UnlockedConclusions().Select(c => c.id).ToList();
            Assert.That(unlocked, Does.Contain("h_complete"));
            Assert.IsTrue(_session.SelectConclusion("h_complete"));
            Assert.That(_session.SelectedConclusionId, Is.EqualTo("h_complete"));
        }

        [Test]
        public void Submit_SolvedBoard_MarksSolved_GrantsReward()
        {
            CollectAll();
            _session.DrawLink(new DirectedEdge("a", "b"));
            _session.DrawLink(new DirectedEdge("b", "c"));
            _session.SelectConclusion("h_complete");

            var events = _session.Submit(out var cert);
            Assert.That(cert.IsSolved, Is.True);
            Assert.That(HasEvent(events, ProgressionEventKind.CaseComplete), Is.True);
            Assert.That(_state.BoardSolved, Is.True);
            Assert.That(_state.MaxFocus, Is.EqualTo(110f));
            Assert.That(_state.Focus, Is.EqualTo(110f));
            Assert.That(_state.SubmittedConclusionId, Is.EqualTo("h_complete"));

            // Replay guard: second submit does not re-grant.
            _session.Submit(out _);
            Assert.That(_state.MaxFocus, Is.EqualTo(110f));
        }

        [Test]
        public void Submit_WrongBoard_Rejected_NotSolved()
        {
            CollectAll();
            var events = _session.Submit(out var cert); // h_wrong selected by default
            Assert.That(cert.IsSolved, Is.False);
            Assert.That(HasEvent(events, ProgressionEventKind.WrongConclusionRejected), Is.True);
            Assert.That(_state.SubmittedWrongConclusionIds, Is.EqualTo(new[] { "h_wrong" }));
            Assert.That(_state.BoardSolved, Is.False);
        }

        [Test]
        public void SyncFocus_MirrorsIntoState()
        {
            _session.SyncFocus(42f, 150f);
            Assert.That(_state.Focus, Is.EqualTo(42f));
            Assert.That(_state.MaxFocus, Is.EqualTo(150f));
        }

        [Test]
        public void RestAndClosed_ApplyEngineConstants()
        {
            _state.Focus = 50f;
            _session.ApplyRestTicks(15); // +15
            Assert.That(_state.Focus, Is.EqualTo(65f));
            _session.ApplyClosedMinutes(10); // +5
            Assert.That(_state.Focus, Is.EqualTo(70f));
        }

        [Test]
        public void NodePositions_RoundTrip()
        {
            Assert.IsFalse(_session.TryGetNodePosition("a", out _));
            _session.SetNodePosition("a", 12.5f, -3f);
            _session.SetNodePosition("unknown", 0f, 0f); // ignored
            Assert.IsTrue(_session.TryGetNodePosition("a", out var pos));
            Assert.That(pos.x, Is.EqualTo(12.5f));
            Assert.That(pos.y, Is.EqualTo(-3f));
            Assert.IsFalse(_session.TryGetNodePosition("unknown", out _));
        }

        [Test]
        public void HintTarget_Tier2PointsAtMissingSolutionClue()
        {
            CollectAll();
            _session.DrawLink(new DirectedEdge("a", "b")); // one S* link of two
            string next = _session.HintTargetClue(2);
            Assert.IsNotNull(next);
            // The remaining S* link b->c is missing — the hint points at its endpoint.
            Assert.That(next, Is.EqualTo("b").Or.EqualTo("c"));
            Assert.That(_session.HintTargetClue(3), Is.Not.Null);
            Assert.That(_session.HintTargetClue(1), Is.Null);
        }
    }
}
