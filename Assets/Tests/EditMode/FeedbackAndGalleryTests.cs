using Morsoul.Core;
using Morsoul.Engine;
using NUnit.Framework;

namespace Morsoul.Tests
{
    public class FeedbackAndGalleryTests
    {
        [TearDown]
        public void TearDown()
        {
            EndingGalleryStore.ClearAll();
        }

        [Test]
        public void ClassifyWrongLink_DetectsReversedAndMissingIntermediate()
        {
            var graph = TestGraphs.BuildValid(); // a -> b -> c chain + d, e

            Assert.AreEqual(
                EvidenceGraph.WrongLinkHintKind.Reversed,
                graph.ClassifyWrongLink(new DirectedEdge("b", "a"), out _));

            Assert.AreEqual(
                EvidenceGraph.WrongLinkHintKind.MissingIntermediate,
                graph.ClassifyWrongLink(new DirectedEdge("a", "c"), out string mid));
            Assert.AreEqual("b", mid);

            Assert.AreEqual(
                EvidenceGraph.WrongLinkHintKind.Unrelated,
                graph.ClassifyWrongLink(new DirectedEdge("a", "d"), out _));
        }

        [Test]
        public void EndingGalleryStore_RecordsAndPersistsUnlockedEndings()
        {
            EndingGalleryStore.ClearAll();
            Assert.AreEqual(0, EndingGalleryStore.UnlockedCount);
            Assert.IsFalse(EndingGalleryStore.IsUnlocked(2));

            EndingGalleryStore.RecordUnlocked(2);
            EndingGalleryStore.RecordUnlocked(2); // idempotent
            EndingGalleryStore.RecordUnlocked(3);

            Assert.AreEqual(2, EndingGalleryStore.UnlockedCount);
            Assert.IsFalse(EndingGalleryStore.IsUnlocked(1));
            Assert.IsTrue(EndingGalleryStore.IsUnlocked(2));
            Assert.IsTrue(EndingGalleryStore.IsUnlocked(3));
        }
    }
}
