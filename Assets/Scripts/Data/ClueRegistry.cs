using System.Collections.Generic;
using UnityEngine;

namespace Morsoul.Data
{
    /// <summary>
    /// Generated index of every ClueData asset in the project, id -&gt; asset.
    /// Lives under Assets/Resources so session restore can rehydrate clue
    /// cards from saved ids at runtime. Rebuild via
    /// Morsoul &gt; Data &gt; Rebuild Clue Registry after adding case content.
    /// </summary>
    public class ClueRegistry : ScriptableObject
    {
        public List<ClueData> clues = new List<ClueData>();

        Dictionary<string, ClueData> _byId;

        /// <summary>Finds a clue by id; null when unknown.</summary>
        public ClueData Find(string clueId)
        {
            if (string.IsNullOrEmpty(clueId)) return null;
            if (_byId == null)
            {
                _byId = new Dictionary<string, ClueData>();
                foreach (var clue in clues)
                    if (clue != null && !string.IsNullOrEmpty(clue.id))
                        _byId[clue.id] = clue;
            }
            return _byId.TryGetValue(clueId, out var found) ? found : null;
        }
    }
}
