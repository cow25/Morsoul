using System;
using System.Collections.Generic;
using Morsoul.Data;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Scene-scoped clue store for hub scenes. Lives on the HubContext
    /// GameObject (no static singleton — access via HubContext). Registering a
    /// clue is idempotent: duplicates are dropped and the event only fires for
    /// genuinely new clues.
    /// </summary>
    public class HubJournal : MonoBehaviour
    {
        readonly List<ClueData> _collected = new List<ClueData>();

        /// <summary>Clues collected so far in this hub, in registration order.</summary>
        public IReadOnlyList<ClueData> Collected => _collected;

        /// <summary>Fires once per newly registered clue (never for duplicates).</summary>
        public event Action<ClueData> ClueRegistered;

        /// <summary>Registers the clue if not already present. Returns true when added.</summary>
        public bool Register(ClueData clue)
        {
            if (clue == null || _collected.Contains(clue)) return false;
            _collected.Add(clue);
            ClueRegistered?.Invoke(clue);
            return true;
        }

        /// <summary>True when the clue was already registered.</summary>
        public bool Contains(ClueData clue) => clue != null && _collected.Contains(clue);
    }
}
