using Morsoul.Core;

namespace Morsoul.Hub
{
    /// <summary>Shared Prologue transition gates for scene adapters and tests.</summary>
    public static class PrologueTransitionRules
    {
        /// <summary>True only on the loaded 2D approach before first contact starts.</summary>
        public static bool CanTriggerElizaIntro(SessionState session) =>
            session != null &&
            session.PrologueExitLoaded &&
            !session.ElizaIntroSeen &&
            string.IsNullOrEmpty(session.ActiveCutsceneId);

        /// <summary>True when the solved board and authored gate unlock allow the 3D seam.</summary>
        public static bool CanCrossRuinsGate(SessionState session, int boardCaseId)
        {
            if (session == null || !session.PrologueGateUnlocked) return false;
            var board = session.GetOrCreateBoardState(boardCaseId);
            return board != null && board.BoardSolved;
        }
    }
}
