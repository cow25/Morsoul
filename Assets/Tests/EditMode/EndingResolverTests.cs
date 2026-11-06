using Morsoul.Engine;
using NUnit.Framework;

namespace Morsoul.Tests
{
    /// <summary>
    /// Algorithm 3 — Ending Resolver: gate-order precedence over all input
    /// combinations, boundary Focus values and the reason/profile output.
    /// </summary>
    public class EndingResolverTests
    {
        static EndingResolverInput In(float focus, bool error, bool solved, int logs) =>
            new EndingResolverInput
            {
                PreErrorFocus = focus,
                IncorrectLink = error,
                BoardSolved = solved,
                LogsCollected = logs,
            };

        [Test]
        public void DepletedFocusPlusError_YieldsEnding1_RegardlessOfBoard()
        {
            foreach (bool solved in new[] { false, true })
            foreach (int logs in new[] { 0, 4 })
            {
                var r = EndingResolver.Resolve(In(10f, true, solved, logs));
                Assert.That(r.Status, Is.EqualTo(EndingResolverStatus.Ok));
                Assert.That(r.Ending, Is.EqualTo(1));
                Assert.That(r.ReasonCode, Is.EqualTo(EndingReasonCode.FocusDepletedError));
            }
        }

        [Test]
        public void DepletedFocus_Alone_DoesNotFail()
        {
            // Focus < 20 without an erroneous link is not an ending trigger.
            var r = EndingResolver.Resolve(In(0f, false, true, 4));
            Assert.That(r.Ending, Is.EqualTo(3));
        }

        [Test]
        public void Boundary_FocusTwenty_IsAffordable()
        {
            var r = EndingResolver.Resolve(In(20f, true, true, 4));
            Assert.That(r.Ending, Is.EqualTo(3)); // 20 is NOT < 20
        }

        [Test]
        public void UnsolvedBoard_NotResolved_UnlessGateOne()
        {
            var r = EndingResolver.Resolve(In(50f, true, false, 4));
            Assert.That(r.Status, Is.EqualTo(EndingResolverStatus.NotResolved));
            Assert.That(r.Ending, Is.Null);
            Assert.That(r.ReasonCode, Is.EqualTo(EndingReasonCode.BoardNotSolved));
        }

        [Test]
        public void Solved_IncompleteLogs_Ending2()
        {
            foreach (int logs in new[] { 0, 1, 2, 3 })
            {
                var r = EndingResolver.Resolve(In(100f, false, true, logs));
                Assert.That(r.Ending, Is.EqualTo(2));
                Assert.That(r.ReasonCode, Is.EqualTo(EndingReasonCode.LogsIncomplete));
            }
        }

        [Test]
        public void Solved_AllLogs_Ending3()
        {
            var r = EndingResolver.Resolve(In(100f, false, true, 4));
            Assert.That(r.Ending, Is.EqualTo(3));
            Assert.That(r.ReasonCode, Is.EqualTo(EndingReasonCode.AllConditionsMet));
        }

        [Test]
        public void Result_EchoesInputProfile()
        {
            var input = In(15f, true, false, 2);
            var r = EndingResolver.Resolve(input);
            Assert.That(r.Profile, Is.SameAs(input));
            Assert.That(r.Profile.PreErrorFocus, Is.EqualTo(15f));
        }
    }
}
