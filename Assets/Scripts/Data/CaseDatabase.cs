using System.Collections.Generic;
using UnityEngine;

namespace Morsoul.Data
{
    /// <summary>
    /// Generated index of every CaseData asset in the project, caseId -> asset.
    /// Lives under Assets/Resources so the case loop can resolve the session's
    /// current case at runtime without an inspector reference. Rebuild via
    /// Morsoul > Data > Rebuild Case Database after adding case content.
    /// </summary>
    public class CaseDatabase : ScriptableObject
    {
        public List<CaseData> cases = new List<CaseData>();

        Dictionary<int, CaseData> _byId;

        /// <summary>Finds a case by numeric id; null when unknown.</summary>
        public CaseData Find(int caseId)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<int, CaseData>();
                foreach (var entry in cases)
                    if (entry != null && !_byId.ContainsKey(entry.caseId))
                        _byId[entry.caseId] = entry;
            }
            return _byId.TryGetValue(caseId, out var found) ? found : null;
        }
    }
}
