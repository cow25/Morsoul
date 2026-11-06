using System;
using System.Collections.Generic;
using Morsoul.Engine;
using NUnit.Framework;

namespace Morsoul.Tests
{
    /// <summary>
    /// Algorithm 4 — canonical state hash: float rounding, -0.0 and NaN
    /// handling, recursive key sorting, compact UTF-8 output and seal
    /// determinism.
    /// </summary>
    public class CanonicalStateTests
    {
        static readonly byte[] Key = System.Text.Encoding.UTF8.GetBytes("test-key");

        [Test]
        public void Canon_SortsKeysRecursively_CompactSeparators()
        {
            var payload = new Dictionary<string, object>
            {
                ["b"] = 1,
                ["a"] = new Dictionary<string, object>
                {
                    ["z"] = "s",
                    ["y"] = 2,
                },
            };
            Assert.AreEqual("{\"a\":{\"y\":2,\"z\":\"s\"},\"b\":1}",
                CanonicalState.Canonicalize(payload));
        }

        [Test]
        public void Canon_RoundsFloats_NormalisesNegativeZero()
        {
            var payload = new List<object> { 1.23449, 1.2345, -0.0, 2.0, 0.0004 };
            Assert.AreEqual("[1.234,1.234,0,2,0]",
                CanonicalState.Canonicalize(payload));
        }

        [Test]
        public void Canon_RejectsNaNAndInfinity()
        {
            Assert.Throws<ArgumentException>(() =>
                CanonicalState.Canonicalize(new List<object> { double.NaN }));
            Assert.Throws<ArgumentException>(() =>
                CanonicalState.Canonicalize(new List<object> { double.PositiveInfinity }));
        }

        [Test]
        public void Canon_EscapesControls_KeepsUtf8()
        {
            var payload = new List<object> { "a\nb\t\"q\"\\", "héllo→" };
            string canon = CanonicalState.Canonicalize(payload);
            Assert.AreEqual("[\"a\\nb\\t\\\"q\\\"\\\\\",\"héllo→\"]", canon);
        }

        [Test]
        public void Canon_RendersEnumsAsNames()
        {
            var payload = new List<object> { ConclusionTier.Complete, SupportBand.Hot };
            Assert.AreEqual("[\"Complete\",\"Hot\"]", CanonicalState.Canonicalize(payload));
        }

        [Test]
        public void StateHash_EqualStates_EqualDigests()
        {
            object A() => new Dictionary<string, object>
            {
                ["caseId"] = 1,
                ["links"] = new List<object>
                {
                    new Dictionary<string, object> { ["sourceId"] = "a", ["targetId"] = "b" },
                },
                ["focus"] = new Dictionary<string, object> { ["energy"] = 80.0 },
            };
            Assert.AreEqual(CanonicalState.ComputeStateHash(A()),
                CanonicalState.ComputeStateHash(A()));
            Assert.AreEqual(64, CanonicalState.ComputeStateHash(A()).Length);
        }

        [Test]
        public void StateHash_SubRoundingChanges_MayShareDigest()
        {
            // Lemma 7: a change smaller than the rounding step does not alter the digest.
            var a = new Dictionary<string, object> { ["v"] = 1.0001 };
            var b = new Dictionary<string, object> { ["v"] = 1.0002 };
            Assert.AreEqual(CanonicalState.ComputeStateHash(a),
                CanonicalState.ComputeStateHash(b));
            var c = new Dictionary<string, object> { ["v"] = 1.001 };
            Assert.AreNotEqual(CanonicalState.ComputeStateHash(a),
                CanonicalState.ComputeStateHash(c));
        }

        [Test]
        public void Seal_BindsProfileVersionAndHash()
        {
            var payload = new Dictionary<string, object> { ["v"] = 1 };
            CanonicalState.HashAndSeal(payload, "local", 2, Key,
                out string hash, out string seal);

            Assert.AreEqual(64, hash.Length);
            Assert.AreEqual(64, seal.Length);
            // Same inputs reseal identically; different inputs reseal differently.
            Assert.AreEqual(seal, CanonicalState.ComputeSeal("local", 2, hash, Key));
            Assert.AreNotEqual(seal, CanonicalState.ComputeSeal("other", 2, hash, Key));
            Assert.AreNotEqual(seal, CanonicalState.ComputeSeal("local", 3, hash, Key));
        }
    }
}
