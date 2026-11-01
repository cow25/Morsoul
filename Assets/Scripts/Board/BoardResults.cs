using Morsoul.Data;

namespace Morsoul.Board
{
    public enum PairingOutcome { OpensPrompt, Rejected, Invalid, AlreadyValidated }

    public readonly struct PairingResult
    {
        public readonly PairingOutcome Outcome;
        public readonly InquiryNodeData Node;
        public readonly string Message;
        public readonly float FocusCost;
        public readonly int StrikeCost;

        public PairingResult(PairingOutcome outcome, InquiryNodeData node, string message,
            float focusCost, int strikeCost)
        {
            Outcome = outcome;
            Node = node;
            Message = message;
            FocusCost = focusCost;
            StrikeCost = strikeCost;
        }
    }

    public enum AnswerOutcome { Correct, Wrong }

    public readonly struct AnswerResult
    {
        public readonly AnswerOutcome Outcome;
        public readonly InquiryNodeData Node;
        public readonly PremiseData Premise;
        public readonly float FocusCost;
        public readonly int StrikeCost;
        public readonly int MissCount;
        public readonly string Hint;

        public AnswerResult(AnswerOutcome outcome, InquiryNodeData node, PremiseData premise,
            float focusCost, int strikeCost, int missCount, string hint)
        {
            Outcome = outcome;
            Node = node;
            Premise = premise;
            FocusCost = focusCost;
            StrikeCost = strikeCost;
            MissCount = missCount;
            Hint = hint;
        }
    }

    public enum HypothesisOutcome { NotReady, UnlocksInterrogation, RoutesToEnding }

    public readonly struct HypothesisResult
    {
        public readonly HypothesisOutcome Outcome;
        public readonly InterrogationData Interrogation;

        public HypothesisResult(HypothesisOutcome outcome, InterrogationData interrogation)
        {
            Outcome = outcome;
            Interrogation = interrogation;
        }
    }
}
