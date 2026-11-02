using System.Collections.Generic;

namespace Morsoul.Hub
{
    /// <summary>
    /// Pure gate-decision logic for <see cref="GateInteractable"/>, extracted
    /// so the flag check can be exercised in EditMode tests without a scene
    /// load.
    /// </summary>
    public static class GateRule
    {
        /// <summary>
        /// A gate is passable when no flag is required, or when the required
        /// flag is present in the set. A null flag set only passes when no
        /// flag is required.
        /// </summary>
        public static bool CanPass(string requiredFlag, ISet<string> flags)
        {
            if (string.IsNullOrEmpty(requiredFlag)) return true;
            return flags != null && flags.Contains(requiredFlag);
        }
    }
}
