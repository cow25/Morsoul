namespace Morsoul.Engine
{
    /// <summary>Algorithm 3 input (Data_Schemas EndingResolverInput).</summary>
    public sealed class EndingResolverInput
    {
        /// <summary>Focus BEFORE the latest link's error charge — never post-charge.</summary>
        public float PreErrorFocus;
        /// <summary>True when the latest drawn link was an error (off S*).</summary>
        public bool IncorrectLink;
        public bool BoardSolved;
        public int LogsCollected; // 0..4
    }

    public enum EndingResolverStatus
    {
        Ok = 0,
        NotResolved = 1,
    }

    public enum EndingReasonCode
    {
        None = 0,
        FocusDepletedError = 1,
        BoardNotSolved = 2,
        LogsIncomplete = 3,
        AllConditionsMet = 4,
    }

    /// <summary>Algorithm 3 output: status, ending (null when not_resolved), reason code, input echo.</summary>
    public sealed class EndingResolverResult
    {
        public EndingResolverStatus Status;
        public int? Ending; // 1, 2 or 3
        public EndingReasonCode ReasonCode;
        public EndingResolverInput Profile;
    }

    /// <summary>
    /// RESOLVE-ENDING (Algorithm.md Algorithm 3): the rule-based decision
    /// tree run after Case 5. Gates in order: depleted-focus error ->
    /// Ending 1; unsolved board -> not_resolved; fewer than 4 logs ->
    /// Ending 2; otherwise Ending 3. Deterministic and total: every input
    /// reaches exactly one leaf.
    /// </summary>
    public static class EndingResolver
    {
        public const int RequiredRunicLogs = 4;

        public static EndingResolverResult Resolve(EndingResolverInput input)
        {
            if (input == null) input = new EndingResolverInput();
            var result = new EndingResolverResult { Profile = input };

            if (input.PreErrorFocus < ProgressionConstants.ErrorFocusCost && input.IncorrectLink)
            {
                result.Status = EndingResolverStatus.Ok;
                result.Ending = 1;
                result.ReasonCode = EndingReasonCode.FocusDepletedError;
            }
            else if (!input.BoardSolved)
            {
                result.Status = EndingResolverStatus.NotResolved;
                result.Ending = null;
                result.ReasonCode = EndingReasonCode.BoardNotSolved;
            }
            else if (input.LogsCollected < RequiredRunicLogs)
            {
                result.Status = EndingResolverStatus.Ok;
                result.Ending = 2;
                result.ReasonCode = EndingReasonCode.LogsIncomplete;
            }
            else
            {
                result.Status = EndingResolverStatus.Ok;
                result.Ending = 3;
                result.ReasonCode = EndingReasonCode.AllConditionsMet;
            }
            return result;
        }
    }
}
