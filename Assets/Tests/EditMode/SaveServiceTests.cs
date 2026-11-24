using System.IO;
using Morsoul.Core;
using Morsoul.Data;
using Morsoul.Engine;
using NUnit.Framework;
using UnityEngine;

namespace Morsoul.Tests
{
    public class SaveServiceTests
    {
        string _dir;
        SaveService _service;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(),
                "MorsoulSaveTests_" + Path.GetRandomFileName());
            _service = new SaveService(Path.Combine(_dir, "checkpoint.json"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        static SaveState SampleState()
        {
            return new SaveState
            {
                playerName = "Vera",
                currentCase = 2,
                checkpointSceneName = "Hub_LowerMarket",
                currentSceneMode = SceneMode.Mode2D,
                currentSceneId = "case02_lower_market_2d",
                playerPosition = new PlayerPositionSave(1.5f, 2.25f, 0f),
                playerFacing2D = PlayerFacing2D.Left,
                collectedClueIds = { "C1_01", "T1_01" },
                boardEdges = { "IN1_01" },
                runicLogsCollected = new System.Collections.Generic.List<bool> { true, false, false, false },
                caseGrades = { Grade.Clean, Grade.Standard },
                uneaseFlags = { new UneaseFlag("Case1_UneaseActive", true) },
                toolsUnlocked = { Tool.GolemEye, Tool.EmotionEcho },
                focusEnergy = 62.5f,
                focusMax = 110f,
                restElapsedRemainderSeconds = 0.06f,
                lastTrustedUtc = "2026-11-07T10:00:00.0000000Z",
                strainedScanUsedByCase =
                    new System.Collections.Generic.List<bool> { false, true, false, false },
                boardStates =
                {
                    new CaseBoardSave
                    {
                        caseId = 2,
                        collectedClueIds = { "C1_02" },
                        links = { new BoardLinkSave("C1_02", "T1_02") },
                        nodePositions = { new NodePositionSave("C1_02", 10f, 20f) },
                        selectedConclusionId = "h_partial",
                        caseErrorCount = 7,
                        firedHintTiers = { 1 },
                        linkingLockedOut = true,
                        submittedConclusionId = "h_complete",
                        submittedWrongConclusionIds = { "h_wrong" },
                        submittedPartialConclusionIds = { "h_partial" },
                        boardSolved = true,
                        completionRewardApplied = true,
                    }
                },
                caseGradeStates =
                {
                    new CaseGradeSave
                    {
                        caseId = 2,
                        caseErrorCount = 7,
                        submittedWrongConclusionIds = { "h_wrong" },
                        submittedPartialConclusionIds = { "h_partial" },
                        interrogationStrikeCount = 2,
                        mistrialUsed = true,
                        strainedScanUsed = true,
                        displayGrade = Grade.Standard,
                    }
                },
                prologueCompleted = true,
                prologueGateUnlocked = true,
                prologueRobeCollected = true,
                prologueExitLoaded = true,
                elizaIntroTriggered = true,
                elizaIntroSeen = true,
                easternRouteUnlocked = true,
                activeCutsceneId = "case02_confession",
                preCutsceneCheckpointId = "checkpoint_case02_hub",
                activeInterrogation = new ActiveInterrogationSave
                {
                    caseId = 2,
                    phaseIndex = 1,
                    composureSegmentsRemaining = 2,
                    strikeCount = 1,
                    preInterrogationCheckpointId = "checkpoint_case02_pre_interrogation",
                },
            };
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void OptionalContexts_RoundTripWithoutInventingZeroValuedObjects(bool position, bool interrogation)
        {
            var state = SampleState();
            if (!position) state.playerPosition = null;
            if (!interrogation) state.activeInterrogation = null;
            Assert.IsTrue(SaveSerializer.TryDeserialize(SaveSerializer.Serialize(state), out var loaded, out var error), error);
            Assert.AreEqual(position, loaded.playerPosition != null);
            Assert.AreEqual(interrogation, loaded.activeInterrogation != null);
            Assert.AreEqual(position, loaded.hasPlayerPosition);
            Assert.AreEqual(interrogation, loaded.hasActiveInterrogation);
            Assert.IsTrue(SaveCanonicalMap.ToCanonical(loaded).ContainsKey("hasPlayerPosition"));
            Assert.IsTrue(SaveCanonicalMap.ToCanonical(loaded).ContainsKey("hasActiveInterrogation"));
        }

        [Test]
        public void SealedVersionThree_VerifiesOriginalMapBeforeMigration()
        {
            var state = SampleState();
            state.saveVersion = 3;
            Assert.IsTrue(SaveSerializer.TryDeserialize(SaveSerializer.Serialize(state), out var loaded, out var error), error);
            Assert.AreEqual(SaveState.CurrentVersion, loaded.saveVersion);
            Assert.IsTrue(loaded.hasPlayerPosition);
            Assert.IsTrue(loaded.hasActiveInterrogation);
            Assert.AreEqual(state.playerPosition.x, loaded.playerPosition.x);
        }

        void WriteLegacyFile(string payload)
        {
            string signature;
            using (var hmac = new System.Security.Cryptography.HMACSHA256(
                       System.Text.Encoding.UTF8.GetBytes("Morsoul-Worlds-Case/Checkpoint/2026")))
            {
                byte[] hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
                var sb = new System.Text.StringBuilder();
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                signature = sb.ToString();
            }
            Directory.CreateDirectory(_dir);
            string escaped = payload.Replace("\\", "\\\\").Replace("\"", "\\\"");
            File.WriteAllText(Path.Combine(_dir, "checkpoint.json"),
                "{\"payload\":\"" + escaped + "\",\"signature\":\"" + signature + "\"}");
        }

        void WriteSealedV2File(SaveState state, bool legacyStrainedScanUsed)
        {
            string payload = JsonUtility.ToJson(state);
            // SaveState no longer carries the v2 global flag; inject the old
            // wire field so the migration reads the same payload shape.
            payload = payload.Substring(0, payload.Length - 1) +
                      ",\"strainedScanUsed\":" +
                      (legacyStrainedScanUsed ? "true" : "false") + "}";
            string hash = CanonicalState.ComputeStateHash(
                SaveCanonicalMap.ToCanonicalForVerification(
                    state, legacyStrainedScanUsed));
            string seal = CanonicalState.ComputeSeal(
                state.profileId ?? "local", state.saveVersion, hash,
                System.Text.Encoding.UTF8.GetBytes(
                    "Morsoul-Worlds-Case/Checkpoint/2026"));
            Directory.CreateDirectory(_dir);
            string escaped = payload.Replace("\\", "\\\\").Replace("\"", "\\\"");
            File.WriteAllText(Path.Combine(_dir, "checkpoint.json"),
                "{\"payload\":\"" + escaped + "\",\"stateHash\":\"" + hash +
                "\",\"seal\":\"" + seal + "\"}");
        }

        [Test]
        public void CanonicalMap_CoversEveryV3SaveField()
        {
            var state = SampleState();
            state.saveVersion = 3;
            var map = SaveCanonicalMap.ToCanonical(state);
            CollectionAssert.AreEquivalent(new[]
            {
                "saveVersion", "profileId", "playerName", "currentCase",
                "checkpointSceneName", "currentSceneMode", "currentSceneId",
                "playerPosition", "playerFacing2D", "collectedClueIds",
                "boardEdges", "runicLogsCollected", "caseGrades", "uneaseFlags",
                "toolsUnlocked", "focusState", "boardStates", "caseGradeStates",
                "prologueCompleted", "prologueGateUnlocked",
                "prologueRobeCollected", "prologueExitLoaded",
                "elizaIntroTriggered", "elizaIntroSeen", "easternRouteUnlocked",
                "activeCutsceneId", "preCutsceneCheckpointId",
                "activeInterrogation",
            }, map.Keys);

            var focus = (System.Collections.Generic.Dictionary<string, object>)
                map["focusState"];
            CollectionAssert.AreEquivalent(new[]
            {
                "currentEnergy", "maximumEnergy", "restElapsedRemainder",
                "closedAtUtc", "lastTrustedUtc",
                "lastOfflineRecoveryAppliedAtUtc", "strainedScanUsedByCase",
            }, focus.Keys);

            var boards = (System.Collections.Generic.List<object>)map["boardStates"];
            var board = (System.Collections.Generic.Dictionary<string, object>)boards[0];
            CollectionAssert.AreEquivalent(new[]
            {
                "caseId", "visibleClueIds", "collectedClueIds", "links",
                "nodePositions", "selectedConclusionId", "caseErrorCount",
                "firedHintTiers", "linkingLockedOut", "submittedConclusionId",
                "submittedWrongConclusionIds", "submittedPartialConclusionIds",
                "boardSolved", "completionRewardApplied",
            }, board.Keys);

            var grades = (System.Collections.Generic.List<object>)map["caseGradeStates"];
            var grade = (System.Collections.Generic.Dictionary<string, object>)grades[0];
            CollectionAssert.AreEquivalent(new[]
            {
                "caseId", "caseErrorCount", "submittedWrongConclusionIds",
                "submittedPartialConclusionIds", "interrogationStrikeCount",
                "mistrialUsed", "strainedScanUsed", "displayGrade",
            }, grade.Keys);

            var position = (System.Collections.Generic.Dictionary<string, object>)
                map["playerPosition"];
            CollectionAssert.AreEquivalent(new[] { "x", "y", "z" }, position.Keys);
            var interrogation = (System.Collections.Generic.Dictionary<string, object>)
                map["activeInterrogation"];
            CollectionAssert.AreEquivalent(new[]
            {
                "caseId", "phaseIndex", "composureSegmentsRemaining",
                "strikeCount", "entryStrikeCount", "preInterrogationCheckpointId",
            }, interrogation.Keys);
        }

        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            _service.Save(SampleState());
            Assert.IsTrue(_service.HasSave);
            Assert.IsTrue(_service.TryLoad(out var loaded, out string error), error);

            Assert.AreEqual("Vera", loaded.playerName);
            Assert.AreEqual(2, loaded.currentCase);
            Assert.AreEqual("Hub_LowerMarket", loaded.checkpointSceneName);
            Assert.AreEqual(SceneMode.Mode2D, loaded.currentSceneMode);
            Assert.AreEqual("case02_lower_market_2d", loaded.currentSceneId);
            Assert.AreEqual(1.5f, loaded.playerPosition.x, 0.001f);
            Assert.AreEqual(2.25f, loaded.playerPosition.y, 0.001f);
            Assert.AreEqual(PlayerFacing2D.Left, loaded.playerFacing2D);
            CollectionAssert.AreEqual(new[] { "C1_01", "T1_01" }, loaded.collectedClueIds);
            CollectionAssert.AreEqual(new[] { "IN1_01" }, loaded.boardEdges);
            CollectionAssert.AreEqual(new[] { true, false, false, false },
                loaded.runicLogsCollected);
            CollectionAssert.AreEqual(new[]
                { Grade.Clean, Grade.Standard, Grade.Ungraded, Grade.Ungraded },
                loaded.caseGrades);
            Assert.AreEqual(1, loaded.uneaseFlags.Count);
            Assert.AreEqual("Case1_UneaseActive", loaded.uneaseFlags[0].key);
            Assert.IsTrue(loaded.uneaseFlags[0].value);
            CollectionAssert.AreEqual(new[] { Tool.GolemEye, Tool.EmotionEcho },
                loaded.toolsUnlocked);
            Assert.AreEqual(62.5f, loaded.focusEnergy, 0.001f);
            Assert.AreEqual(110f, loaded.focusMax);
            Assert.AreEqual(0.06f, loaded.restElapsedRemainderSeconds);
            Assert.AreEqual("2026-11-07T10:00:00.0000000Z", loaded.lastTrustedUtc);
            CollectionAssert.AreEqual(new[] { false, true, false, false },
                loaded.strainedScanUsedByCase);
            Assert.AreEqual(1, loaded.boardStates.Count);
            var board = loaded.boardStates[0];
            Assert.AreEqual(2, board.caseId);
            CollectionAssert.AreEqual(new[] { "C1_02" }, board.collectedClueIds);
            Assert.AreEqual(1, board.links.Count);
            Assert.AreEqual("C1_02", board.links[0].sourceId);
            Assert.AreEqual("T1_02", board.links[0].targetId);
            Assert.AreEqual(1, board.nodePositions.Count);
            Assert.AreEqual(20f, board.nodePositions[0].y);
            Assert.AreEqual("h_partial", board.selectedConclusionId);
            Assert.AreEqual(7, board.caseErrorCount);
            CollectionAssert.AreEqual(new[] { 1 }, board.firedHintTiers);
            Assert.IsTrue(board.linkingLockedOut);
            Assert.AreEqual("h_complete", board.submittedConclusionId);
            CollectionAssert.AreEqual(new[] { "h_wrong" }, board.submittedWrongConclusionIds);
            CollectionAssert.AreEqual(new[] { "h_partial" }, board.submittedPartialConclusionIds);
            Assert.IsTrue(board.boardSolved);
            Assert.IsTrue(board.completionRewardApplied);
            var grade = loaded.caseGradeStates[0];
            Assert.AreEqual(2, grade.caseId);
            Assert.AreEqual(7, grade.caseErrorCount);
            CollectionAssert.AreEqual(new[] { "h_wrong" }, grade.submittedWrongConclusionIds);
            CollectionAssert.AreEqual(new[] { "h_partial" }, grade.submittedPartialConclusionIds);
            Assert.AreEqual(2, grade.interrogationStrikeCount);
            Assert.IsTrue(grade.mistrialUsed);
            Assert.IsTrue(grade.strainedScanUsed);
            Assert.AreEqual(Grade.Standard, grade.displayGrade);
            Assert.IsTrue(loaded.prologueCompleted);
            Assert.IsTrue(loaded.prologueGateUnlocked);
            Assert.IsTrue(loaded.prologueRobeCollected);
            Assert.IsTrue(loaded.prologueExitLoaded);
            Assert.IsTrue(loaded.elizaIntroTriggered);
            Assert.IsTrue(loaded.elizaIntroSeen);
            Assert.IsTrue(loaded.easternRouteUnlocked);
            Assert.AreEqual("case02_confession", loaded.activeCutsceneId);
            Assert.AreEqual("checkpoint_case02_hub", loaded.preCutsceneCheckpointId);
            Assert.IsNotNull(loaded.activeInterrogation);
            Assert.AreEqual(1, loaded.activeInterrogation.phaseIndex);
            Assert.AreEqual(2, loaded.activeInterrogation.composureSegmentsRemaining);
            Assert.AreEqual(1, loaded.activeInterrogation.strikeCount);
            Assert.AreEqual("checkpoint_case02_pre_interrogation",
                loaded.activeInterrogation.preInterrogationCheckpointId);
        }

        [Test]
        public void TamperedPayload_HashMismatch()
        {
            _service.Save(SampleState());
            string path = Path.Combine(_dir, "checkpoint.json");
            string json = File.ReadAllText(path);
            // Flip a value inside the payload: "currentCase":2 -> 7.
            json = json.Replace("currentCase\\\":2", "currentCase\\\":7");
            Assert.AreNotEqual(json, File.ReadAllText(path));
            File.WriteAllText(path, json);

            Assert.IsFalse(_service.TryLoad(out _, out string error));
            StringAssert.Contains("hash", error);
        }

        [Test]
        public void TamperedSeal_Rejected()
        {
            _service.Save(SampleState());
            string path = Path.Combine(_dir, "checkpoint.json");
            string json = File.ReadAllText(path);
            File.WriteAllText(path, json.Replace("\"seal\":\"", "\"seal\":\"0"));

            Assert.IsFalse(_service.TryLoad(out _, out string error));
            StringAssert.Contains("modified", error);
        }

        [Test]
        public void LegacyV1File_VerifiesAndMigrates()
        {
            // Fabricate a true v1 checkpoint: only v1 fields in the payload,
            // {"payload","signature"} signed with the legacy HMAC.
            string payload = "{\"playerName\":\"Legacy\",\"currentCase\":3," +
                "\"checkpointSceneName\":\"Hub_Wilderness\",\"focusPercent\":41," +
                "\"strainedScanUsed\":true}";
            WriteLegacyFile(payload);

            Assert.IsTrue(_service.TryLoad(out var loaded, out string error), error);
            Assert.AreEqual(SaveState.CurrentVersion, loaded.saveVersion);
            Assert.AreEqual(41f, loaded.focusEnergy); // focusPercent migrated to energy
            Assert.AreEqual(100f, loaded.focusMax);
            Assert.IsTrue(loaded.strainedScanUsedByCase[2]); // legacy global -> current case
        }

        [Test]
        public void LegacyV1CompletedFlags_GrandfatherBoardCompletion()
        {
            string payload = "{\"playerName\":\"Legacy\",\"currentCase\":5," +
                "\"focusPercent\":95,\"case1Completed\":true," +
                "\"case4Completed\":true,\"case5Completed\":true}";
            WriteLegacyFile(payload);

            Assert.IsTrue(_service.TryLoad(out var loaded, out string error), error);
            Assert.AreEqual(120f, loaded.focusMax); // two Cases 1–4 rewards only
            Assert.AreEqual(95f, loaded.focusEnergy);
            Assert.AreEqual(3, loaded.boardStates.Count);
            foreach (int caseId in new[] { 1, 4, 5 })
            {
                var board = loaded.boardStates.Find(b => b.caseId == caseId);
                Assert.IsNotNull(board);
                Assert.IsTrue(board.boardSolved);
                Assert.AreEqual(caseId != 5, board.completionRewardApplied);
                Assert.AreEqual(0, board.caseErrorCount);
                Assert.AreEqual(0, board.links.Count);
            }
            Assert.AreEqual(Grade.Standard, loaded.caseGrades[0]);
            Assert.AreEqual(Grade.Standard, loaded.caseGrades[3]);
            Assert.AreEqual(Grade.Ungraded, loaded.caseGrades[1]);
            Assert.AreEqual(Grade.Ungraded, loaded.caseGrades[2]);
        }

        [Test]
        public void SealedV2File_VerifiesAndMigrates()
        {
            var state = new SaveState
            {
                saveVersion = 2,
                playerName = "Sealed",
                currentCase = 3,
                checkpointSceneName = "Hub_Wilderness",
                focusEnergy = 41f,
                focusMax = 100f,
                closedAtUtc = "2026-11-07T10:00:00.0000000Z",
                lastOfflineRecoveryAppliedAtUtc = "2026-11-07T09:00:00.0000000Z",
            };
            WriteSealedV2File(state, legacyStrainedScanUsed: true);

            Assert.IsTrue(_service.TryLoad(out var loaded, out string error), error);
            Assert.AreEqual(SaveState.CurrentVersion, loaded.saveVersion);
            Assert.AreEqual("Hub_Wilderness", loaded.currentSceneId);
            Assert.AreEqual("2026-11-07T10:00:00.0000000Z", loaded.lastTrustedUtc);
            Assert.IsTrue(loaded.strainedScanUsedByCase[2]);
            Assert.AreEqual(4, loaded.caseGradeStates.Count);
        }

        [Test]
        public void MalformedFile_Rejected_NotThrown()
        {
            string path = Path.Combine(_dir, "checkpoint.json");
            Directory.CreateDirectory(_dir);
            File.WriteAllText(path, "{ this is not json !!!");
            Assert.IsFalse(_service.TryLoad(out _, out string error));
            Assert.IsNotNull(error);
        }

        [Test]
        public void MissingFile_HasSaveFalse_TryLoadFails()
        {
            Assert.IsFalse(_service.HasSave);
            Assert.IsFalse(_service.TryLoad(out _, out _));
        }

        [Test]
        public void Overwrite_ReplacesPriorState()
        {
            _service.Save(SampleState());
            var second = SampleState();
            second.playerName = "Odo";
            second.currentCase = 5;
            _service.Save(second);
            Assert.IsTrue(_service.TryLoad(out var loaded, out _));
            Assert.AreEqual("Odo", loaded.playerName);
            Assert.AreEqual(5, loaded.currentCase);
        }

        [Test]
        public void DeleteSave_RemovesFile()
        {
            _service.Save(SampleState());
            _service.DeleteSave();
            Assert.IsFalse(_service.HasSave);
        }

        [Test]
        public void NewSaveState_GeneratesGuidProfileId_InsteadOfLocal()
        {
            var state = new SaveState();
            Assert.AreNotEqual("local", state.profileId);
            Assert.IsTrue(System.Guid.TryParse(state.profileId, out _));

            state.profileId = "";
            string json = SaveSerializer.Serialize(state);
            Assert.IsTrue(System.Guid.TryParse(state.profileId, out _));
            Assert.IsTrue(SaveSerializer.TryDeserialize(json, out var loaded, out _));
            Assert.AreEqual(state.profileId, loaded.profileId);
        }

        [Test]
        public void GameLock_ResolveDatabasePath_AcceptsUserAndDbArguments()
        {
            string baseDir = Path.Combine(_dir, "morsoul_db_test");
            Directory.CreateDirectory(baseDir);

            Assert.AreEqual(
                Path.Combine(baseDir, "game.db"),
                GameLock.ResolveDatabasePath(new[] { "Morsoul.exe" }, baseDir));

            Assert.AreEqual(
                Path.Combine(baseDir, "game_alice.db"),
                GameLock.ResolveDatabasePath(new[] { "Morsoul.exe", "--user", "alice" }, baseDir));

            Assert.AreEqual(
                Path.Combine(baseDir, "bob.db"),
                GameLock.ResolveDatabasePath(new[] { "Morsoul.exe", "--user=bob.db" }, baseDir));

            string directUserDb = Path.Combine(baseDir, "charlie.db");
            File.WriteAllText(directUserDb, "");
            Assert.AreEqual(
                directUserDb,
                GameLock.ResolveDatabasePath(new[] { "Morsoul.exe", "--user", "charlie" }, baseDir));

            string explicitDb = Path.Combine(baseDir, "custom.db");
            Assert.AreEqual(
                explicitDb,
                GameLock.ResolveDatabasePath(new[] { "Morsoul.exe", "--db", explicitDb }, baseDir));
        }
    }
}
