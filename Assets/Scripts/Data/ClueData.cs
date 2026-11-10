using UnityEngine;

namespace Morsoul.Data
{
    [CreateAssetMenu(menuName = "Morsoul/Clue")]
    public class ClueData : EvidenceData
    {
        public string displayName;
        public ClueType type;
        public int caseId;
        public AcquisitionMethod acquisition;
        public Tool requiredTool;
        public string requiredFlag;
        [TextArea] public string description;
        public string dissonanceKeyword;
        public bool isBacktrackingOnly;
        public bool hasWorldPosition;
        public Vector2 worldPosition;
    }
}
