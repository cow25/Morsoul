using UnityEngine;

namespace Morsoul.Data
{
    [CreateAssetMenu(menuName = "Morsoul/Premise")]
    public class PremiseData : EvidenceData
    {
        public string title;
        [TextArea] public string proposition;
    }
}
