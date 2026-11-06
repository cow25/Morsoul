using Morsoul.Core;
using Morsoul.Data;
using NUnit.Framework;
using UnityEngine;

namespace Morsoul.Tests
{
    public class SessionStateTests
    {
        SessionState _session;

        [SetUp]
        public void SetUp() => _session = new SessionState();

        [Test]
        public void RegisterClueId_Dedupes()
        {
            Assert.IsTrue(_session.RegisterClueId("C1_01"));
            Assert.IsFalse(_session.RegisterClueId("C1_01"));
            Assert.AreEqual(1, _session.ClueIds.Count);
        }

        [Test]
        public void Flags_SetAndHas()
        {
            _session.SetFlag("Case1_UneaseActive");
            Assert.IsTrue(_session.HasFlag("Case1_UneaseActive"));
            Assert.IsFalse(_session.HasFlag("missing"));
            _session.SetFlag("off", false);
            Assert.IsFalse(_session.HasFlag("off"));
        }

        [Test]
        public void MergeBoardEdges_Dedupes()
        {
            _session.MergeBoardEdges(new[] { "N1", "N2", "N1" });
            _session.MergeBoardEdges(new[] { "N2", "N3" });
            CollectionAssert.AreEqual(new[] { "N1", "N2", "N3" }, _session.BoardEdges);
        }

        [Test]
        public void MarkRunicLog_InRangeOnly()
        {
            _session.MarkRunicLog(0);
            _session.MarkRunicLog(9); // ignored
            _session.MarkRunicLog(-1); // ignored
            Assert.AreEqual(1, _session.RunicLogCount);
            Assert.IsTrue(_session.RunicLogs[0]);
        }

        [Test]
        public void ApplyThenRestore_RoundTrips()
        {
            _session.PlayerName = "Vera";
            _session.CurrentCase = 3;
            _session.RegisterClueId("C1_03");
            _session.RegisterClueId("T1_03");
            _session.SetFlag("Case1_UneaseActive");
            _session.LatentLogCollected = true;
            _session.MergeBoardEdges(new[] { "IN1_03" });
            _session.MarkRunicLog(2);
            _session.SetGrade(1, Grade.Clean);
            _session.SetGrade(3, Grade.Standard);
            _session.UnlockTool(Tool.EmotionEcho);

            var save = new SaveState();
            _session.ApplyTo(save);

            var restored = new SessionState();
            restored.RestoreFrom(save);

            Assert.AreEqual("Vera", restored.PlayerName);
            Assert.AreEqual(3, restored.CurrentCase);
            CollectionAssert.AreEquivalent(new[] { "C1_03", "T1_03" }, restored.ClueIds);
            Assert.IsTrue(restored.HasFlag("Case1_UneaseActive"));
            Assert.IsTrue(restored.LatentLogCollected);
            CollectionAssert.AreEqual(new[] { "IN1_03" }, restored.BoardEdges);
            Assert.AreEqual(1, restored.RunicLogCount);
            Assert.IsTrue(restored.RunicLogs[2]);
            Assert.AreEqual(Grade.Clean, restored.Grades[1]);
            Assert.AreEqual(Grade.Standard, restored.Grades[3]);
            CollectionAssert.Contains(restored.Tools, Tool.EmotionEcho);
        }

        [Test]
        public void RestoreFrom_WithResolver_RehydratesClueAssets()
        {
            var clue = ScriptableObject.CreateInstance<ClueData>();
            clue.id = "C1_01";
            var save = new SaveState();
            save.collectedClueIds.Add("C1_01");
            save.collectedClueIds.Add("GHOST_99");

            _session.RestoreFrom(save, id => id == "C1_01" ? clue : null);

            Assert.IsTrue(_session.ClueAssets.ContainsKey("C1_01"));
            Assert.AreSame(clue, _session.ClueAssets["C1_01"]);
            Assert.IsTrue(_session.HasClue("GHOST_99")); // id kept, no asset
        }

        [Test]
        public void ApplyTo_WritesFourGradeSlots()
        {
            _session.SetGrade(2, Grade.Flawed);
            var save = new SaveState();
            _session.ApplyTo(save);
            Assert.AreEqual(4, save.caseGrades.Count);
            Assert.AreEqual(Grade.Flawed, save.caseGrades[1]);
            Assert.AreEqual(Grade.Ungraded, save.caseGrades[0]);
        }

        [Test]
        public void RegisterLatentClue_MarksPerCaseFlag_AndRunicSlot()
        {
            var clue = ScriptableObject.CreateInstance<ClueData>();
            clue.id = "L4_04";
            clue.caseId = 4;
            try
            {
                Assert.IsTrue(_session.RegisterLatentClue(clue));
                Assert.IsTrue(_session.IsLatentLogCollected(4));
                Assert.IsFalse(_session.IsLatentLogCollected(3), "per-case, not global");
                Assert.IsTrue(_session.RunicLogs[3], "L4 -> slot 3");
                Assert.AreEqual(1, _session.RunicLogCount);
                Assert.IsTrue(_session.LatentLogCollected, "legacy latch kept in step");
                Assert.IsFalse(_session.RegisterLatentClue(clue), "log already marked");
            }
            finally { Object.DestroyImmediate(clue); }
        }

        [Test]
        public void TryParseRunicLogSlot_ConventionOnly()
        {
            Assert.IsTrue(SessionState.TryParseRunicLogSlot("L1_01", out int s1));
            Assert.AreEqual(0, s1);
            Assert.IsTrue(SessionState.TryParseRunicLogSlot("L4_04", out int s4));
            Assert.AreEqual(3, s4);
            Assert.IsFalse(SessionState.TryParseRunicLogSlot("L9_09", out _));
            Assert.IsFalse(SessionState.TryParseRunicLogSlot("C1_01", out _));
            Assert.IsFalse(SessionState.TryParseRunicLogSlot(null, out _));
        }

        [Test]
        public void WasMistrialRetried_PerCaseFlag()
        {
            _session.SetFlag(SessionState.MistrialFlagFor(2));
            Assert.IsTrue(_session.WasMistrialRetried(2));
            Assert.IsFalse(_session.WasMistrialRetried(3));
        }

        [Test]
        public void StrainedScan_PerCaseFlag()
        {
            _session.MarkStrainedScan(2);
            Assert.IsTrue(_session.IsStrainedScanUsed(2));
            Assert.IsFalse(_session.IsStrainedScanUsed(3));
        }

        [Test]
        public void BoardState_SaveRestoreRoundTrip()
        {
            var board = _session.GetOrCreateBoardState(2);
            board.VisibleClueIds.AddRange(new[] { "a", "b" });
            board.CollectedClueIds.Add("a");
            board.Links.Add(new Morsoul.Engine.DirectedEdge("a", "b"));
            board.NodePositions["a"] = new Morsoul.Engine.NodePosition { x = 5f, y = -3f };
            board.SelectedConclusionId = "h_partial";
            board.CaseErrorCount = 7;
            board.FiredHintTiers.Add(2);
            board.LinkingLockedOut = true;
            board.SubmittedConclusionId = "h_complete";
            board.BoardSolved = true;
            board.CompletionRewardApplied = true;

            var save = new SaveState();
            _session.ApplyTo(save);
            var restored = new SessionState();
            restored.RestoreFrom(save);

            var rb = restored.GetOrCreateBoardState(2);
            CollectionAssert.AreEqual(new[] { "a", "b" }, rb.VisibleClueIds);
            CollectionAssert.AreEqual(new[] { "a" }, rb.CollectedClueIds);
            Assert.AreEqual(1, rb.Links.Count);
            Assert.AreEqual(new Morsoul.Engine.DirectedEdge("a", "b"), rb.Links[0]);
            Assert.AreEqual(-3f, rb.NodePositions["a"].y);
            Assert.AreEqual("h_partial", rb.SelectedConclusionId);
            Assert.AreEqual(7, rb.CaseErrorCount);
            CollectionAssert.AreEqual(new[] { 2 }, rb.FiredHintTiers);
            Assert.IsTrue(rb.LinkingLockedOut);
            Assert.AreEqual("h_complete", rb.SubmittedConclusionId);
            Assert.IsTrue(rb.BoardSolved);
            Assert.IsTrue(rb.CompletionRewardApplied);
        }

        [Test]
        public void RestoreFrom_LegacyStrainedScan_MapsToCurrentCase()
        {
            var save = new SaveState { currentCase = 3, strainedScanUsed = true };
            _session.RestoreFrom(save);
            Assert.IsTrue(_session.IsStrainedScanUsed(3));
            Assert.IsFalse(_session.IsStrainedScanUsed(2));
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            _session.PlayerName = "x";
            _session.RegisterClueId("C1");
            _session.SetFlag("f");
            _session.MergeBoardEdges(new[] { "N" });
            _session.MarkRunicLog(0);
            _session.SetGrade(1, Grade.Clean);
            _session.UnlockTool(Tool.GolemEye);
            _session.Reset();
            Assert.AreEqual("", _session.PlayerName);
            Assert.AreEqual(0, _session.ClueIds.Count);
            Assert.AreEqual(0, _session.Flags.Count);
            Assert.AreEqual(0, _session.BoardEdges.Count);
            Assert.AreEqual(0, _session.RunicLogCount);
            Assert.AreEqual(0, _session.Grades.Count);
            Assert.AreEqual(0, _session.Tools.Count);
            Assert.AreEqual(0, _session.BoardStates.Count);
        }
    }
}
