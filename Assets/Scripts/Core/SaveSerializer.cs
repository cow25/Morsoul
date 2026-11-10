using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Morsoul.Data;
using Morsoul.Engine;
using UnityEngine;

namespace Morsoul.Core
{
    /// <summary>
    /// JSON checkpoint serialization sealed per Algorithm.md B1:
    ///
    ///   canonical JSON (sorted keys, floats rounded to 3 decimals,
    ///   compact separators, UTF-8) -> SHA-256 state_hash ->
    ///   seal = HMAC-SHA256(key, "profile_id:save_version:state_hash").
    ///
    /// Wire format: {"payload": "<state json>", "stateHash": "<hex>",
    /// "seal": "<hex>"}. The key is a client-side constant until the
    /// launcher holds a real secret — tamper-evident, not tamper-proof.
    ///
    /// Version-1 files ({"payload","signature"}) verify against the legacy
    /// per-payload HMAC and migrate on load. Explicit completed-case flags
    /// are the only proof of a grandfathered completion; old board counters
    /// and scores never mark a weighted board solved.
    /// </summary>
    public static class SaveSerializer
    {
        const string Salt = "Morsoul-Worlds-Case/Checkpoint/2026";
        static readonly byte[] SealKey = Encoding.UTF8.GetBytes(Salt);

        [Serializable]
        class CheckpointFile
        {
            public string payload;
            public string stateHash;
            public string seal;
            public string signature = null; // legacy v1; populated only by FromJson
        }

        /// <summary>Fields that existed only in pre-v3 payloads.</summary>
        [Serializable]
#pragma warning disable CS0649 // populated by JsonUtility on old payloads
        class LegacySavePayload
        {
            public float focusPercent = 100f;
            public bool strainedScanUsed;
            public bool[] completedCases;
            public int[] completedCaseIds;
            public bool case0Completed;
            public bool case1Completed;
            public bool case2Completed;
            public bool case3Completed;
            public bool case4Completed;
            public bool case5Completed;
            public bool completedCase0;
            public bool completedCase1;
            public bool completedCase2;
            public bool completedCase3;
            public bool completedCase4;
            public bool completedCase5;
            public bool case0Solved;
            public bool case1Solved;
            public bool case2Solved;
            public bool case3Solved;
            public bool case4Solved;
            public bool case5Solved;
        }
#pragma warning restore CS0649

        /// <summary>Serializes the state and seals it per Algorithm B1.</summary>
        public static string Serialize(SaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.saveVersion >= 4)
            {
                state.hasPlayerPosition = state.playerPosition != null;
                state.hasActiveInterrogation = state.activeInterrogation != null;
                state.hasInvestigationReturn = state.investigationReturn != null;
            }
            string payload = JsonUtility.ToJson(state);
            CanonicalState.HashAndSeal(SaveCanonicalMap.ToCanonical(state),
                state.profileId ?? "local", state.saveVersion, SealKey,
                out string hash, out string seal);
            return JsonUtility.ToJson(new CheckpointFile
            {
                payload = payload,
                stateHash = hash,
                seal = seal,
            });
        }

        /// <summary>
        /// Verifies the seal (or the legacy signature) and deserializes.
        /// Returns false with an error reason on malformed input or tampered
        /// payload; never throws on bad file contents.
        /// </summary>
        public static bool TryDeserialize(string fileJson, out SaveState state, out string error)
        {
            state = null;
            error = null;
            if (string.IsNullOrEmpty(fileJson))
            {
                error = "empty checkpoint";
                return false;
            }

            CheckpointFile file;
            try
            {
                file = JsonUtility.FromJson<CheckpointFile>(fileJson);
            }
            catch (Exception e)
            {
                error = $"checkpoint not valid JSON: {e.Message}";
                return false;
            }

            if (file == null || file.payload == null)
            {
                error = "checkpoint missing payload";
                return false;
            }

            try
            {
                state = JsonUtility.FromJson<SaveState>(file.payload);
            }
            catch (Exception e)
            {
                error = $"payload not valid state JSON: {e.Message}";
                return false;
            }

            if (state == null)
            {
                error = "payload deserialized to null";
                return false;
            }

            if (state.saveVersion >= 4)
            {
                if (!state.hasPlayerPosition) state.playerPosition = null;
                if (!state.hasActiveInterrogation) state.activeInterrogation = null;
                if (!state.hasInvestigationReturn) state.investigationReturn = null;
            }

            LegacySavePayload legacy = null;
            try
            {
                legacy = JsonUtility.FromJson<LegacySavePayload>(file.payload);
            }
            catch (Exception)
            {
                legacy = null;
            }

            bool legacySignature = !string.IsNullOrEmpty(file.signature) &&
                                   string.IsNullOrEmpty(file.stateHash);
            if (legacySignature)
            {
                if (!FixedTimeEquals(LegacySign(file.payload), file.signature))
                {
                    error = "signature mismatch — checkpoint was modified";
                    return false;
                }
                return MigrateLoaded(state, legacy, forceVersionOne: true, out error);
            }

            if (string.IsNullOrEmpty(file.stateHash) || string.IsNullOrEmpty(file.seal))
            {
                error = "checkpoint missing state hash or seal";
                return false;
            }

            string computedHash;
            try
            {
                computedHash = CanonicalState.ComputeStateHash(
                    SaveCanonicalMap.ToCanonicalForVerification(
                        state, legacy != null && legacy.strainedScanUsed));
            }
            catch (Exception e)
            {
                error = $"payload cannot be canonicalized: {e.Message}";
                return false;
            }

            if (!FixedTimeEquals(computedHash, file.stateHash))
            {
                error = "state hash mismatch — checkpoint was modified";
                return false;
            }

            string expectedSeal = CanonicalState.ComputeSeal(
                state.profileId ?? "local", state.saveVersion, file.stateHash, SealKey);
            if (!FixedTimeEquals(expectedSeal, file.seal))
            {
                error = "seal mismatch — checkpoint was modified";
                return false;
            }
            return MigrateLoaded(state, legacy, forceVersionOne: false, out error);
        }

        static bool MigrateLoaded(SaveState state, LegacySavePayload legacy,
            bool forceVersionOne, out string error)
        {
            error = null;
            if (forceVersionOne) state.saveVersion = 1;
            if (state.saveVersion > SaveState.CurrentVersion)
            {
                error = $"checkpoint version {state.saveVersion} is newer than supported version {SaveState.CurrentVersion}";
                return false;
            }
            if (state.saveVersion < 2) MigrateVersion1(state, legacy);
            if (state.saveVersion < 3) MigrateVersion2(state, legacy);
            if (state.saveVersion < 4)
            {
                state.hasPlayerPosition = state.playerPosition != null;
                state.hasActiveInterrogation = state.activeInterrogation != null;
                state.investigationReturn = null;
                state.hasInvestigationReturn = false;
                state.investigationCaseId = -1;
            }
            state.saveVersion = SaveState.CurrentVersion;
            NormalizeCurrent(state);
            return true;
        }

        /// <summary>v1 -> weighted-board defaults. Old board internals stay inert.</summary>
        static void MigrateVersion1(SaveState state, LegacySavePayload legacy)
        {
            bool[] completed = CompletedCases(legacy);
            state.profileId = string.IsNullOrEmpty(state.profileId) ? "local" : state.profileId;
            state.focusMax = FocusTuning.FocusMax;
            for (int caseId = 1; caseId <= 4; caseId++)
                if (completed[caseId])
                    state.focusMax += ProgressionConstants.CompleteSubmissionCapacityBonus;
            state.focusEnergy = Mathf.Clamp(
                legacy != null ? legacy.focusPercent : FocusTuning.FocusMax,
                0f, state.focusMax);
            state.restElapsedRemainderSeconds = 0f;
            state.closedAtUtc = "";
            state.lastTrustedUtc = "";
            state.lastOfflineRecoveryAppliedAtUtc = "";
            state.boardEdges = new List<string>();
            state.boardStates = new List<CaseBoardSave>();

            for (int caseId = 0; caseId <= 5; caseId++)
            {
                if (!completed[caseId]) continue;
                var board = GetOrCreateBoard(state, caseId);
                board.boardSolved = true;
                board.completionRewardApplied = caseId >= 1 && caseId <= 4;
            }

            state.caseGrades = FourGrades(state.caseGrades);
            for (int caseId = 1; caseId <= 4; caseId++)
                if (completed[caseId])
                    state.caseGrades[caseId - 1] = Grade.Standard;

            state.strainedScanUsedByCase = FourBools(state.strainedScanUsedByCase);
            if (legacy != null && legacy.strainedScanUsed &&
                state.currentCase >= 1 && state.currentCase <= 4)
                state.strainedScanUsedByCase[state.currentCase - 1] = true;
        }

        /// <summary>v2 -> v3: add persistent clock/context/grade/history state.</summary>
        static void MigrateVersion2(SaveState state, LegacySavePayload legacy)
        {
            NormalizeCurrent(state);
            state.currentSceneId = string.IsNullOrEmpty(state.currentSceneId)
                ? state.checkpointSceneName ?? ""
                : state.currentSceneId;
            state.boardEdges = new List<string>();

            DateTime? trusted = ParseUtc(state.lastTrustedUtc);
            DateTime? closed = ParseUtc(state.closedAtUtc);
            DateTime? applied = ParseUtc(state.lastOfflineRecoveryAppliedAtUtc);
            if (!trusted.HasValue || (closed.HasValue && closed.Value > trusted.Value)) trusted = closed;
            if (!trusted.HasValue || (applied.HasValue && applied.Value > trusted.Value)) trusted = applied;
            state.lastTrustedUtc = FormatUtc(trusted);
            state.closedAtUtc = FormatUtc(closed);
            state.lastOfflineRecoveryAppliedAtUtc = FormatUtc(applied);

            state.strainedScanUsedByCase = FourBools(state.strainedScanUsedByCase);
            for (int caseId = 1; caseId <= 4; caseId++)
                if (HasFlag(state.uneaseFlags, SessionState.StrainedScanFlagFor(caseId)))
                    state.strainedScanUsedByCase[caseId - 1] = true;
            if (legacy != null && legacy.strainedScanUsed &&
                state.currentCase >= 1 && state.currentCase <= 4)
                state.strainedScanUsedByCase[state.currentCase - 1] = true;

            state.focusMax = Math.Max(1f, state.focusMax);
            state.focusEnergy = Mathf.Clamp(state.focusEnergy, 0f, state.focusMax);
            EnsureCaseGradeStates(state);
            state.saveVersion = 3;
        }

        static void NormalizeCurrent(SaveState state)
        {
            state.profileId = string.IsNullOrEmpty(state.profileId) ? "local" : state.profileId;
            state.playerName ??= "";
            state.checkpointSceneName ??= "";
            state.currentSceneId ??= "";
            state.collectedClueIds ??= new List<string>();
            state.boardEdges ??= new List<string>();
            state.runicLogsCollected = FourBools(state.runicLogsCollected);
            state.caseGrades = FourGrades(state.caseGrades);
            state.uneaseFlags ??= new List<UneaseFlag>();
            state.toolsUnlocked ??= new List<Tool>();
            state.strainedScanUsedByCase = FourBools(state.strainedScanUsedByCase);
            state.boardStates ??= new List<CaseBoardSave>();
            state.caseGradeStates ??= new List<CaseGradeSave>();
            state.activeCutsceneId ??= "";
            state.preCutsceneCheckpointId ??= "";
            state.focusMax = Math.Max(1f, state.focusMax);
            state.focusEnergy = Mathf.Clamp(state.focusEnergy, 0f, state.focusMax);
            state.restElapsedRemainderSeconds = Math.Max(0f, state.restElapsedRemainderSeconds);
            EnsureCaseGradeStates(state);
        }

        static bool[] CompletedCases(LegacySavePayload legacy)
        {
            var completed = new bool[6];
            if (legacy == null) return completed;
            if (legacy.completedCases != null)
                for (int i = 0; i < legacy.completedCases.Length && i < completed.Length; i++)
                    completed[i] = legacy.completedCases[i];
            if (legacy.completedCaseIds != null)
                foreach (int caseId in legacy.completedCaseIds)
                    if (caseId >= 0 && caseId < completed.Length)
                        completed[caseId] = true;
            completed[0] |= legacy.case0Completed || legacy.completedCase0 || legacy.case0Solved;
            completed[1] |= legacy.case1Completed || legacy.completedCase1 || legacy.case1Solved;
            completed[2] |= legacy.case2Completed || legacy.completedCase2 || legacy.case2Solved;
            completed[3] |= legacy.case3Completed || legacy.completedCase3 || legacy.case3Solved;
            completed[4] |= legacy.case4Completed || legacy.completedCase4 || legacy.case4Solved;
            completed[5] |= legacy.case5Completed || legacy.completedCase5 || legacy.case5Solved;
            return completed;
        }

        static CaseBoardSave GetOrCreateBoard(SaveState state, int caseId)
        {
            foreach (var board in state.boardStates)
                if (board != null && board.caseId == caseId)
                    return board;
            var created = new CaseBoardSave { caseId = caseId };
            state.boardStates.Add(created);
            return created;
        }

        static void EnsureCaseGradeStates(SaveState state)
        {
            for (int caseId = 1; caseId <= 4; caseId++)
            {
                CaseGradeSave gradeState = null;
                foreach (var existing in state.caseGradeStates)
                    if (existing != null && existing.caseId == caseId)
                    {
                        gradeState = existing;
                        break;
                    }
                if (gradeState == null)
                {
                    gradeState = new CaseGradeSave { caseId = caseId };
                    state.caseGradeStates.Add(gradeState);
                }

                CaseBoardSave board = null;
                foreach (var existing in state.boardStates)
                    if (existing != null && existing.caseId == caseId)
                    {
                        board = existing;
                        break;
                    }
                if (board != null)
                {
                    gradeState.caseErrorCount = board.caseErrorCount;
                    gradeState.submittedWrongConclusionIds = Unique(
                        board.submittedWrongConclusionIds);
                    gradeState.submittedPartialConclusionIds = Unique(
                        board.submittedPartialConclusionIds);
                }
                gradeState.strainedScanUsed = gradeState.strainedScanUsed ||
                    (state.strainedScanUsedByCase.Count > caseId - 1 &&
                     state.strainedScanUsedByCase[caseId - 1]);
                gradeState.mistrialUsed = gradeState.mistrialUsed || HasFlag(
                    state.uneaseFlags, SessionState.MistrialFlagFor(caseId));
                if (gradeState.displayGrade != Grade.Ungraded)
                {
                    state.caseGrades[caseId - 1] = gradeState.displayGrade;
                }
                else if (state.caseGrades.Count > caseId - 1)
                {
                    gradeState.displayGrade = state.caseGrades[caseId - 1];
                }
            }
        }

        static List<string> Unique(List<string> source)
        {
            var result = new List<string>();
            foreach (string value in source ?? new List<string>())
                if (!string.IsNullOrEmpty(value) && !result.Contains(value))
                    result.Add(value);
            return result;
        }

        static List<bool> FourBools(List<bool> source)
        {
            var result = new List<bool> { false, false, false, false };
            if (source != null)
                for (int i = 0; i < source.Count && i < result.Count; i++)
                    result[i] = source[i];
            return result;
        }

        static List<Grade> FourGrades(List<Grade> source)
        {
            var result = new List<Grade>
            {
                Grade.Ungraded, Grade.Ungraded, Grade.Ungraded, Grade.Ungraded,
            };
            if (source != null)
                for (int i = 0; i < source.Count && i < result.Count; i++)
                    result[i] = source[i];
            return result;
        }

        static bool HasFlag(List<UneaseFlag> flags, string key)
        {
            foreach (var flag in flags ?? new List<UneaseFlag>())
                if (flag != null && flag.key == key) return flag.value;
            return false;
        }

        static DateTime? ParseUtc(string iso)
        {
            return !string.IsNullOrEmpty(iso) &&
                   DateTime.TryParse(iso, null,
                       System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed)
                ? parsed
                : (DateTime?)null;
        }

        static string FormatUtc(DateTime? utc) =>
            utc.HasValue ? utc.Value.ToString("o") : "";

        static string LegacySign(string payload)
        {
            using (var hmac = new HMACSHA256(SealKey))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
