using System;

namespace Morsoul.Gameplay
{
    public static class EndingSelector
    {
        public const int RequiredRunicLogs = 4;

        public static int Select(bool boardFailed, int runicLogsCollected)
        {
            if (runicLogsCollected < 0)
                throw new ArgumentOutOfRangeException(nameof(runicLogsCollected));
            if (boardFailed) return 1;
            return runicLogsCollected >= RequiredRunicLogs ? 3 : 2;
        }
    }
}
