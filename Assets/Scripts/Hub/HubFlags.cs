using System.Collections.Generic;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Scene-scoped set of story/progress flags for a hub scene. Lives on the
    /// HubContext GameObject; wired into <see cref="Morsoul.Dialogue.DialogueController"/>
    /// via SetFlags so requiresFlag gating shares this exact set.
    /// </summary>
    public class HubFlags : MonoBehaviour
    {
        readonly HashSet<string> _flags = new HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>The backing flag set; hand to DialogueController.SetFlags.</summary>
        public ISet<string> Set => _flags;

        /// <summary>True when the flag has been set (exact, ordinal match).</summary>
        public bool Has(string flag) =>
            !string.IsNullOrEmpty(flag) && _flags.Contains(flag);

        /// <summary>Marks the flag as set; no-op for null/empty names.</summary>
        public void Add(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) _flags.Add(flag);
        }
    }
}
