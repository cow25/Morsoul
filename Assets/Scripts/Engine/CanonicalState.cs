using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Morsoul.Engine
{
    /// <summary>
    /// Algorithm 4 — canonical state hash and seal (Algorithm.md B1).
    ///
    /// Canon: every float rounded to 3 decimals (-0.0 -> 0.0, NaN/Infinity
    /// rejected), object keys sorted recursively, compact separators, UTF-8.
    /// state_hash = SHA-256(canon); seal = HMAC-SHA256(key,
    /// "profile_id:save_version:state_hash"). Equal states give equal
    /// digests; a change below 0.0005 may not alter the digest (Lemma 7).
    /// </summary>
    public static class CanonicalState
    {
        /// <summary>
        /// Serializes a payload tree (null, bool, string, numbers, enums,
        /// IDictionary, IEnumerable) to canonical JSON. Throws
        /// ArgumentException on NaN/Infinity or unhandled types.
        /// </summary>
        public static string Canonicalize(object payload)
        {
            var sb = new StringBuilder();
            Write(payload, sb);
            return sb.ToString();
        }

        /// <summary>SHA-256 hex digest of the canonical serialization.</summary>
        public static string ComputeStateHash(object payload)
        {
            string canon = Canonicalize(payload);
            using (var sha = SHA256.Create())
                return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(canon)));
        }

        /// <summary>HMAC-SHA256 hex over "profile_id:save_version:state_hash".</summary>
        public static string ComputeSeal(string profileId, int saveVersion,
            string stateHash, byte[] key)
        {
            if (key == null || key.Length == 0)
                throw new ArgumentException("seal key required", nameof(key));
            string message = $"{profileId}:{saveVersion}:{stateHash}";
            using (var hmac = new HMACSHA256(key))
                return ToHex(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)));
        }

        /// <summary>Convenience: state hash + seal in one call.</summary>
        public static void HashAndSeal(object payload, string profileId, int saveVersion,
            byte[] key, out string stateHash, out string seal)
        {
            stateHash = ComputeStateHash(payload);
            seal = ComputeSeal(profileId, saveVersion, stateHash, key);
        }

        // --- Canonical JSON writer ----------------------------------------

        static void Write(object value, StringBuilder sb)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    return;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    return;
                case string s:
                    WriteString(s, sb);
                    return;
                case char c:
                    WriteString(c.ToString(), sb);
                    return;
                case float f:
                    WriteNumber(f, sb);
                    return;
                case double d:
                    WriteNumber(d, sb);
                    return;
                case decimal m:
                    WriteNumber((double)m, sb);
                    return;
                case Enum e:
                    WriteString(e.ToString(), sb);
                    return;
                case sbyte or byte or short or ushort or int or uint or long or ulong:
                    sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
                case IDictionary map:
                    WriteObject(map, sb);
                    return;
                case IEnumerable seq:
                    WriteArray(seq, sb);
                    return;
                default:
                    throw new ArgumentException(
                        $"Type {value.GetType().Name} is not JSON-serialisable for canonical state.");
            }
        }

        static void WriteNumber(double value, StringBuilder sb)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentException("NaN and Infinity are rejected in canonical state.");
            double rounded = Math.Round(value, 3, MidpointRounding.ToEven);
            if (rounded == 0.0) rounded = 0.0; // normalize -0.0
            sb.Append(rounded.ToString("0.###", CultureInfo.InvariantCulture));
        }

        static void WriteObject(IDictionary map, StringBuilder sb)
        {
            var sorted = new SortedDictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in map)
                sorted[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] = entry.Value;

            sb.Append('{');
            bool first = true;
            foreach (var pair in sorted)
            {
                if (!first) sb.Append(',');
                first = false;
                WriteString(pair.Key, sb);
                sb.Append(':');
                Write(pair.Value, sb);
            }
            sb.Append('}');
        }

        static void WriteArray(IEnumerable seq, StringBuilder sb)
        {
            sb.Append('[');
            bool first = true;
            foreach (object item in seq)
            {
                if (!first) sb.Append(',');
                first = false;
                Write(item, sb);
            }
            sb.Append(']');
        }

        static void WriteString(string s, StringBuilder sb)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c); // ensure_ascii=false: raw UTF-8
                        break;
                }
            }
            sb.Append('"');
        }

        static string ToHex(byte[] hash)
        {
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
