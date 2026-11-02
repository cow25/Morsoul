using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Morsoul.Core
{
    /// <summary>
    /// JSON checkpoint serialization sealed with HMAC-SHA256 (GDD §6.8).
    /// Wire format: {"payload": "<state json>", "signature": "<hmac hex>"}.
    /// The salt is client-side, so this is tamper-evident rather than
    /// tamper-proof — an acknowledged limitation per the GDD threat model.
    /// </summary>
    public static class SaveSerializer
    {
        const string Salt = "Morsoul-Worlds-Case/Checkpoint/2026";

        [Serializable]
        class CheckpointFile
        {
            public string payload;
            public string signature;
        }

        /// <summary>Serializes the state and seals it with an HMAC signature.</summary>
        public static string Serialize(SaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            string payload = JsonUtility.ToJson(state);
            return JsonUtility.ToJson(new CheckpointFile
            {
                payload = payload,
                signature = Sign(payload)
            });
        }

        /// <summary>
        /// Verifies the signature and deserializes. Returns false (with an
        /// error reason) on malformed input or tampered payload; never throws
        /// on bad file contents.
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

            if (file == null || file.payload == null || file.signature == null)
            {
                error = "checkpoint missing payload or signature";
                return false;
            }

            string expected = Sign(file.payload);
            if (!FixedTimeEquals(expected, file.signature))
            {
                error = "signature mismatch — checkpoint was modified";
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
            return true;
        }

        static string Sign(string payload)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Salt)))
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
