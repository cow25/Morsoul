using System.Linq;
using Morsoul.Data;

namespace Morsoul.Core
{
    public static class ClueAcquisition
    {
        public static bool CanAcquire(ClueData clue, SessionState session,
            RunicLogData log, out string reason)
        {
            reason = null;
            if (clue == null || string.IsNullOrEmpty(clue.id) || session == null)
                reason = "Clue or session is unavailable.";
            else if (!string.IsNullOrEmpty(clue.requiredFlag) && !session.HasFlag(clue.requiredFlag))
                reason = "Required story state is missing: " + clue.requiredFlag;
            else if (!HasTool(session, clue.requiredTool))
                reason = "Required tool is locked: " + clue.requiredTool;
            else if (clue.isBacktrackingOnly && session.CurrentCase <= 4)
                reason = "This clue requires a post-Case-4 revisit.";
            else if (log != null && (log.caseId != clue.caseId || session.CurrentCase < log.availableAfterCase))
                reason = "The runic log is not available yet.";
            else if (log != null && !HasTool(session, log.requiredTool))
                reason = "The runic log requires: " + log.requiredTool;
            else
            {
                switch (clue.acquisition)
                {
                    case AcquisitionMethod.GolemEyeScan:
                        if (!HasTool(session, Tool.GolemEye)) reason = "Golem Eye is locked.";
                        break;
                    case AcquisitionMethod.EmotionEcho:
                        if (!HasTool(session, Tool.EmotionEcho)) reason = "Emotion Echo is locked.";
                        break;
                    case AcquisitionMethod.AetherSight:
                        if (!HasTool(session, Tool.AetherSight)) reason = "Aether Sight is locked.";
                        break;
                    case AcquisitionMethod.AetherSightPlusGolemEye:
                        if (!HasTool(session, Tool.AetherSight) || !HasTool(session, Tool.GolemEye))
                            reason = "Aether Sight and Golem Eye are both required.";
                        break;
                }
            }
            return reason == null;
        }

        static bool HasTool(SessionState session, Tool tool) =>
            tool == Tool.None || session.Tools.Contains(tool);
    }
}
