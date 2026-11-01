using Morsoul.Data;

namespace Morsoul.Interrogation
{
    public enum InterrogationStatus { InProgress, Confessed, Mistrial }

    public enum MoveOutcome { PressResponse, Correct, Wrong, Locked }

    public readonly struct MoveResult
    {
        public readonly MoveOutcome Outcome;
        public readonly MoveData Move;
        public readonly string Response;
        public readonly int PhaseIndex;
        public readonly bool SegmentBroken;
        public readonly bool ShieldShattered;
        public readonly bool StrikeCounted;
        public readonly InterrogationStatus Status;

        public MoveResult(MoveOutcome outcome, MoveData move, string response, int phaseIndex,
            bool segmentBroken, bool shieldShattered, bool strikeCounted,
            InterrogationStatus status)
        {
            Outcome = outcome;
            Move = move;
            Response = response;
            PhaseIndex = phaseIndex;
            SegmentBroken = segmentBroken;
            ShieldShattered = shieldShattered;
            StrikeCounted = strikeCounted;
            Status = status;
        }
    }
}
