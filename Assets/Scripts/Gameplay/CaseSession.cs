using System;
using Morsoul.Data;

namespace Morsoul.Gameplay
{
    public class CaseSession
    {
        public CaseSession(CaseData caseData)
        {
            Case = caseData ?? throw new ArgumentNullException(nameof(caseData));
        }

        public CaseData Case { get; }
        public int StrikesLost { get; private set; }
        public int WrongCausalAnswers { get; private set; }
        public int CaseErrorCount { get; private set; }
        public bool WrongConclusionSubmitted { get; private set; }
        public bool HintTierFired { get; private set; }
        public bool StrainedScanUsed { get; private set; }
        public bool MistrialRetried { get; private set; }

        public bool IsGraded => Case.caseId >= 1 && Case.caseId <= 4;

        /// <summary>Syncs persisted weighted-board grade inputs before resolution.</summary>
        public void SetBoardGradeInputs(int caseErrorCount,
            bool wrongConclusionSubmitted, bool hintTierFired)
        {
            CaseErrorCount = Math.Max(0, caseErrorCount);
            WrongConclusionSubmitted = wrongConclusionSubmitted;
            HintTierFired = hintTierFired;
        }

        public void RecordWrongCausalAnswer() => WrongCausalAnswers++;

        public void RecordStrikes(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            StrikesLost += count;
        }

        public void RecordStrainedScan() => StrainedScanUsed = true;

        /// <summary>Restores a persisted strike total on a fresh checkpoint session.</summary>
        public void RestoreStrikes(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            StrikesLost = Math.Min(count, 3);
        }

        public void RecordMistrialRetry()
        {
            MistrialRetried = true;
            StrikesLost = 0;
        }

        public Grade ComputeGrade()
        {
            if (!IsGraded) return Grade.Ungraded;
            if (MistrialRetried) return Grade.Flawed;
            if (StrikesLost > 0 || WrongCausalAnswers > 0 || CaseErrorCount > 0 ||
                WrongConclusionSubmitted || HintTierFired || StrainedScanUsed)
                return Grade.Standard;
            return Grade.Clean;
        }
    }
}
