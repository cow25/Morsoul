using System;
using System.Security.Cryptography;
using System.Text;
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
    /// per-payload HMAC and migrate on load: focusPercent becomes
    /// focusEnergy at maximum 100, and the obsolete slot/premise counters
    /// do not populate weighted-board state.
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

        /// <summary>Serializes the state and seals it per Algorithm B1.</summary>
        public static string Serialize(SaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
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

            bool legacy = !string.IsNullOrEmpty(file.signature) &&
                          string.IsNullOrEmpty(file.stateHash);
            if (legacy)
            {
                if (!FixedTimeEquals(LegacySign(file.payload), file.signature))
                {
                    error = "signature mismatch — checkpoint was modified";
                    return false;
                }
                MigrateLegacy(state);
                return true;
            }

            if (string.IsNullOrEmpty(file.stateHash) || string.IsNullOrEmpty(file.seal))
            {
                error = "checkpoint missing state hash or seal";
                return false;
            }

            string computedHash;
            try
            {
                computedHash = CanonicalState.ComputeStateHash(SaveCanonicalMap.ToCanonical(state));
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
            return true;
        }

        /// <summary>v1 -> v2 defaults. Old board counters stay inert.</summary>
        static void MigrateLegacy(SaveState state)
        {
            state.saveVersion = SaveState.CurrentVersion;
            if (string.IsNullOrEmpty(state.profileId)) state.profileId = "local";
            state.focusEnergy = state.focusPercent;
            state.focusMax = FocusTuning.FocusMax;
            state.restElapsedRemainderSeconds = 0f;
            state.closedAtUtc = "";
            state.lastOfflineRecoveryAppliedAtUtc = "";
            if (state.boardStates == null) state.boardStates = new System.Collections.Generic.List<CaseBoardSave>();
        }

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
