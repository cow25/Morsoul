using Morsoul.Data;
using Morsoul.Gameplay;
using NUnit.Framework;

namespace Morsoul.Tests
{
    public class CaseSessionTests
    {
        [TearDown]
        public void TearDown() => TestCases.DestroyAll();

        [Test]
        public void Ctor_Null_Throws()
        {
            Assert.That(() => new CaseSession(null), Throws.ArgumentNullException);
        }

        [Test]
        public void CleanGrade_WithNoMistakes()
        {
            var session = new CaseSession(TestCases.Build(1, true).caseData);
            Assert.That(session.IsGraded, Is.True);
            Assert.That(session.ComputeGrade(), Is.EqualTo(Grade.Clean));
        }

        [Test]
        public void Strikes_MakeStandard()
        {
            var session = new CaseSession(TestCases.Build(1, true).caseData);
            session.RecordStrikes(2);
            Assert.That(session.StrikesLost, Is.EqualTo(2));
            Assert.That(session.ComputeGrade(), Is.EqualTo(Grade.Standard));
        }

        [Test]
        public void WrongCausalAnswer_MakesStandard()
        {
            var session = new CaseSession(TestCases.Build(1, true).caseData);
            session.RecordWrongCausalAnswer();
            Assert.That(session.WrongCausalAnswers, Is.EqualTo(1));
            Assert.That(session.ComputeGrade(), Is.EqualTo(Grade.Standard));
        }

        [Test]
        public void StrainedScan_MakesStandard()
        {
            var session = new CaseSession(TestCases.Build(1, true).caseData);
            session.RecordStrainedScan();
            Assert.That(session.StrainedScanUsed, Is.True);
            Assert.That(session.ComputeGrade(), Is.EqualTo(Grade.Standard));
        }

        [Test]
        public void MistrialRetry_MakesFlawed()
        {
            var session = new CaseSession(TestCases.Build(1, true).caseData);
            session.RecordMistrialRetry();
            Assert.That(session.MistrialRetried, Is.True);
            Assert.That(session.ComputeGrade(), Is.EqualTo(Grade.Flawed));
        }

        [Test]
        public void MistrialRetry_ResetsStrikes()
        {
            var session = new CaseSession(TestCases.Build(1, true).caseData);
            session.RecordStrikes(2);
            session.RecordMistrialRetry();
            Assert.That(session.StrikesLost, Is.EqualTo(0));
        }

        [Test]
        public void PrologueCase_IsUngraded()
        {
            var session = new CaseSession(TestCases.Build(0, false).caseData);
            Assert.That(session.IsGraded, Is.False);
            Assert.That(session.ComputeGrade(), Is.EqualTo(Grade.Ungraded));
        }

        [Test]
        public void BoardOnlyCase_IsUngraded()
        {
            var session = new CaseSession(TestCases.Build(5, false, boardOnly: true).caseData);
            Assert.That(session.IsGraded, Is.False);
            Assert.That(session.ComputeGrade(), Is.EqualTo(Grade.Ungraded));
        }

        [Test]
        public void NegativeStrikes_Throw()
        {
            var session = new CaseSession(TestCases.Build(1, true).caseData);
            Assert.That(() => session.RecordStrikes(-1),
                Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void EndingSelector_BoardFailed_AlwaysEnding1()
        {
            Assert.That(EndingSelector.Select(true, 0), Is.EqualTo(1));
            Assert.That(EndingSelector.Select(true, 4), Is.EqualTo(1));
        }

        [Test]
        public void EndingSelector_AllLogs_Ending3()
        {
            Assert.That(EndingSelector.Select(false, EndingSelector.RequiredRunicLogs), Is.EqualTo(3));
        }

        [Test]
        public void EndingSelector_MissingLogs_Ending2()
        {
            Assert.That(EndingSelector.Select(false, 3), Is.EqualTo(2));
            Assert.That(EndingSelector.Select(false, 0), Is.EqualTo(2));
        }

        [Test]
        public void EndingSelector_NegativeLogs_Throws()
        {
            Assert.That(() => EndingSelector.Select(false, -1),
                Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }
    }
}
