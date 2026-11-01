using UnityEngine;

namespace Morsoul.Data
{
    [CreateAssetMenu(menuName = "Morsoul/Runic Log")]
    public class RunicLogData : ScriptableObject
    {
        public int id;
        public int caseId;
        public string location;
        public Tool requiredTool;
        [TextArea] public string contents;
        public int availableAfterCase;
    }
}
