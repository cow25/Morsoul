using System.Collections.Generic;
using System.Linq;
using Morsoul.Core;
using Morsoul.Data;
using Morsoul.Engine;
using Morsoul.Flow;
using NUnit.Framework;
using UnityEngine;

namespace Morsoul.Tests
{
    public class InvestigationCompletionTests
    {
        readonly List<Object> _assets = new List<Object>();
        CaseData _case;
        SessionState _session;
        FocusModel _focus;

        T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _assets.Add(asset);
            return asset;
        }

        [SetUp]
        public void SetUp()
        {
            _session = new SessionState { CurrentCase = 1 };
            _focus = new FocusModel();
            _focus.Spend(60f);
            _case = Asset<CaseData>();
            _case.caseId = 1;
            _case.hasInvestigationScene = true;
            _case.hasInterrogation = true;
            var def = TestGraphs.FiveClueDef();
            var graph = Asset<EvidenceGraphData>();
            graph.caseId = 1;
            graph.clueIds = def.ClueIds;
            graph.reliabilities = def.Reliabilities;
            graph.authoredClueEdges = def.AuthoredClueEdges;
            graph.solutionEdges = def.SolutionEdges;
            graph.conclusions = def.Conclusions;
            graph.parameters = def.Parameters;
            graph.startingWrongConclusionId = def.StartingWrongConclusionId;
            graph.completeConclusionId = def.CompleteConclusionId;
            _case.evidenceGraph = graph;
            _case.clues = graph.clueIds.Select(id =>
            {
                var clue = Asset<ClueData>();
                clue.id = id;
                clue.caseId = 1;
                return clue;
            }).ToArray();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void Simulation_SubmitsRealSolution_WithoutAdvancingCampaign()
        {
            Assert.IsTrue(InvestigationCompletion.TrySimulate(_case, _session, _focus, out var result), result.Message);
            var state = _session.GetOrCreateBoardState(1);
            Assert.IsTrue(state.BoardSolved);
            Assert.IsTrue(state.CompletionRewardApplied);
            Assert.AreEqual("h_complete", state.SubmittedConclusionId);
            Assert.AreEqual(110f, _focus.Maximum);
            Assert.AreEqual(110f, _focus.Percent);
            Assert.AreEqual(1, _session.CurrentCase);
            Assert.AreEqual(InvestigationCompletionSource.Simulated, _session.GetInvestigationSource(1));
            Assert.IsTrue(_session.WasInvestigationSimulated(1));
            CollectionAssert.AreEquivalent(_case.clues.Select(c => c.id), result.CollectedClueIds);
        }

        [Test]
        public void RepeatedEntry_PreservesSolvedStateAndDoesNotRefillFocus()
        {
            InvestigationCompletion.TrySimulate(_case, _session, _focus, out _);
            var original = _session.GetOrCreateBoardState(1);
            _focus.Spend(25f);
            Assert.IsTrue(InvestigationCompletion.TrySimulate(_case, _session, _focus, out var result));
            Assert.AreSame(original, _session.GetOrCreateBoardState(1));
            Assert.IsFalse(result.DeductionAccepted);
            Assert.IsEmpty(result.CollectedClueIds);
            Assert.AreEqual(85f, _focus.Percent);
            Assert.AreEqual(110f, _focus.Maximum);
        }

        [Test]
        public void MissingRequiredTool_FailsWithoutPartialCollectionOrBoardMutation()
        {
            _case.clues[0].requiredTool = Tool.AetherSight;
            Assert.IsFalse(InvestigationCompletion.TrySimulate(_case, _session, _focus, out _));
            Assert.IsEmpty(_session.ClueIds);
            Assert.IsEmpty(_session.BoardStates);
            Assert.AreEqual(40f, _focus.Percent);
            Assert.IsFalse(_session.WasInvestigationSimulated(1));
        }

        [Test]
        public void OptionalClue_IsCollectedOnlyOnEligibleRevisit_WithoutRewardReplay()
        {
            var latent = Asset<ClueData>();
            latent.id = "L1_01";
            latent.caseId = 1;
            latent.type = ClueType.Latent;
            latent.requiredTool = Tool.AetherSight;
            latent.isBacktrackingOnly = true;
            _case.clues = _case.clues.Concat(new[] { latent }).ToArray();
            Assert.IsTrue(InvestigationCompletion.TrySimulate(_case, _session, _focus, out _));
            Assert.IsFalse(_session.HasClue(latent.id));
            _session.CurrentCase = 5;
            _session.UnlockTool(Tool.AetherSight);
            _focus.Spend(30f);
            Assert.IsTrue(InvestigationCompletion.TrySimulate(_case, _session, _focus, out var result));
            Assert.IsTrue(_session.HasClue(latent.id));
            Assert.AreEqual(1, _session.RunicLogCount);
            Assert.AreEqual(5, _session.CurrentCase);
            Assert.AreEqual(80f, _focus.Percent);
            CollectionAssert.AreEqual(new[] { latent.id }, result.CollectedClueIds);
        }

        [Test]
        public void CombinedToolAndStoryGate_AreBothRequired()
        {
            var clue = _case.clues[0];
            clue.acquisition = AcquisitionMethod.AetherSightPlusGolemEye;
            clue.requiredFlag = "Case4_BrokkFreed";
            _session.UnlockTool(Tool.AetherSight);
            Assert.IsFalse(ClueAcquisition.CanAcquire(clue, _session, null, out _));
            _session.UnlockTool(Tool.GolemEye);
            Assert.IsFalse(ClueAcquisition.CanAcquire(clue, _session, null, out _));
            _session.SetFlag(clue.requiredFlag);
            Assert.IsTrue(ClueAcquisition.CanAcquire(clue, _session, null, out _));
        }

        [Test]
        public void LockedBoard_DoesNotReceiveRecoveryOrLoseItsHistory()
        {
            var board = _session.GetOrCreateBoardState(1);
            board.LinkingLockedOut = true;
            board.CaseErrorCount = 6;
            _focus.Spend(40f);
            Assert.IsFalse(InvestigationCompletion.TrySimulate(_case, _session, _focus, out _));
            Assert.AreSame(board, _session.GetOrCreateBoardState(1));
            Assert.IsTrue(board.LinkingLockedOut);
            Assert.AreEqual(6, board.CaseErrorCount);
            Assert.IsEmpty(_session.ClueIds);
            Assert.AreEqual(0f, _focus.Percent);
        }

        [Test]
        public void ExistingPartialBoard_PreservesHistoryAndNodePositions()
        {
            var board = _session.GetOrCreateBoardState(1);
            board.Links.Add(new DirectedEdge("a", "b"));
            board.VisibleClueIds.AddRange(new[] { "a", "b" });
            board.CollectedClueIds.AddRange(new[] { "a", "b" });
            board.CaseErrorCount = 2;
            board.SubmittedWrongConclusionIds.Add("h_wrong");
            board.NodePositions["a"] = new NodePosition { x = 42, y = 12 };
            Assert.IsTrue(InvestigationCompletion.TrySimulate(_case, _session, _focus, out var result), result.Message);
            var accepted = _session.GetOrCreateBoardState(1);
            Assert.AreEqual(2, accepted.CaseErrorCount);
            CollectionAssert.AreEqual(new[] { "h_wrong" }, accepted.SubmittedWrongConclusionIds);
            Assert.AreEqual(42, accepted.NodePositions["a"].x);
            Assert.AreEqual(2, accepted.Links.Count);
        }

        [Test]
        public void RealSolvedBoard_IsNeverRelabeledOrRewarded()
        {
            var board = _session.GetOrCreateBoardState(1);
            board.BoardSolved = true;
            board.CompletionRewardApplied = true;
            _session.RecordInvestigationSource(1, InvestigationCompletionSource.Real);
            Assert.IsTrue(InvestigationCompletion.TrySimulate(_case, _session, _focus, out _));
            Assert.AreSame(board, _session.GetOrCreateBoardState(1));
            Assert.AreEqual(InvestigationCompletionSource.Real, _session.GetInvestigationSource(1));
            Assert.AreEqual(40f, _focus.Percent);
        }

        [Test]
        public void Provenance_RoundTripsThroughExistingSealedSave()
        {
            InvestigationCompletion.TrySimulate(_case, _session, _focus, out _);
            var save = new SaveState();
            _session.ApplyTo(save);
            Assert.IsTrue(SaveSerializer.TryDeserialize(SaveSerializer.Serialize(save), out var loaded, out var error), error);
            var restored = new SessionState();
            restored.RestoreFrom(loaded);
            Assert.IsTrue(restored.WasInvestigationSimulated(1));
            Assert.AreEqual(InvestigationCompletionSource.Simulated, restored.GetInvestigationSource(1));
            Assert.IsTrue(restored.WasClueSimulated(1, "a"));
        }

        [Test]
        public void InvestigationReturn_RoundTripsOriginWithoutOverwritingInvestigationPose()
        {
            _session.SetPlayerContext(SceneMode.Mode2D, "Hub_CathedralNave",
                new Vector3(4, 7, 0), PlayerFacing2D.Left);
            _session.CaptureInvestigationReturn();
            _session.CurrentCase = 5;
            _session.InvestigationCaseId = 2;
            _session.SetPlayerContext(SceneMode.Mode3D, "Belfry",
                new Vector3(10, 2, 3), PlayerFacing2D.Down);
            var save = new SaveState();
            _session.ApplyTo(save);
            Assert.IsTrue(SaveSerializer.TryDeserialize(SaveSerializer.Serialize(save), out var loaded, out var error), error);
            var restored = new SessionState();
            restored.RestoreFrom(loaded);
            Assert.AreEqual("Hub_CathedralNave", restored.InvestigationReturn.sceneName);
            Assert.AreEqual(4, restored.InvestigationReturn.position.x);
            Assert.AreEqual(7, restored.InvestigationReturn.position.y);
            Assert.AreEqual(PlayerFacing2D.Left, restored.InvestigationReturn.facing);
            Assert.AreEqual("Belfry", restored.CurrentSceneId);
            Assert.AreEqual(5, restored.CurrentCase);
            Assert.AreEqual(2, restored.InvestigationCaseId);
            Assert.AreEqual(new Vector3(10, 2, 3), restored.PlayerPosition);
        }

        [Test]
        public void Finale_RejectsInvestigationSimulation()
        {
            _case.caseId = 5;
            Assert.IsFalse(InvestigationCompletion.TrySimulate(_case, _session, _focus, out _));
            Assert.IsEmpty(_session.ClueIds);
            Assert.IsEmpty(_session.BoardStates);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void AuthoredCase_AcceptsSolutionAndPreservesLockedLogs(int caseId)
        {
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<CaseData>(
                $"Assets/Data/Cases/Case0{caseId}/Case_0{caseId}.asset");
            Assert.IsNotNull(data);
            _session.CurrentCase = caseId;
            _session.UnlockTool(Tool.GolemEye);
            if (caseId >= 3) _session.UnlockTool(Tool.EmotionEcho);
            if (caseId >= 4) _session.UnlockTool(Tool.AetherSight);
            Assert.IsTrue(InvestigationCompletion.TrySimulate(data, _session, _focus, out var result), result.Message);
            Assert.IsTrue(_session.GetOrCreateBoardState(caseId).BoardSolved);
            Assert.AreEqual(caseId == 0 ? 100f : 110f, _focus.Maximum);
            Assert.AreEqual(0, _session.RunicLogCount);
            Assert.AreEqual(caseId, _session.CurrentCase);
        }

        [Test]
        public void InvalidGraph_FailsWithoutMutatingExistingProgress()
        {
            _case.evidenceGraph.solutionEdges = new[] { new DirectedEdge("unknown", "b") };
            _session.RegisterClue(_case.clues[0]);
            Assert.IsFalse(InvestigationCompletion.TrySimulate(_case, _session, _focus, out _));
            CollectionAssert.AreEqual(new[] { "a" }, _session.ClueIds);
            Assert.IsEmpty(_session.BoardStates);
            Assert.AreEqual(40f, _focus.Percent);
        }

        [Test]
        public void IntegrationPolicy_RequiresExplicitStatusAndEveryDevelopmentGate()
        {
            var catalog = Asset<InvestigationIntegrationCatalog>();
            catalog.areas = new[] { new InvestigationArea { caseId = 1, routeKey = "investigation.1", allowDevelopmentFallback = true } };
            Assert.AreEqual(InvestigationEntryKind.Blocked, catalog.Decide("investigation.1", true, false, out _));
            catalog.enableDevelopmentFallback = true;
            Assert.AreEqual(InvestigationEntryKind.Blocked, catalog.Decide("investigation.1", false, true, out _));
            Assert.AreEqual(InvestigationEntryKind.Simulate, catalog.Decide("investigation.1", true, false, out _));
            catalog.areas[0].status = InvestigationIntegrationStatus.Integrated;
            Assert.AreEqual(InvestigationEntryKind.Blocked, catalog.Decide("investigation.1", true, false, out _));
            Assert.AreEqual(InvestigationEntryKind.Load, catalog.Decide("investigation.1", false, true, out _));
            Assert.AreEqual(InvestigationEntryKind.Blocked, catalog.Decide("unknown", true, true, out _));
            catalog.areas = new[] { catalog.areas[0], catalog.areas[0] };
            Assert.AreEqual(InvestigationEntryKind.Blocked, catalog.Decide("investigation.1", true, true, out _));
        }
    }
}
