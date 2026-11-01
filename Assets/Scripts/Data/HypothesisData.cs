using UnityEngine;

namespace Morsoul.Data
{
    [CreateAssetMenu(menuName = "Morsoul/Hypothesis")]
    public class HypothesisData : ScriptableObject
    {
        public string id;
        public PremiseData premise1;
        public PremiseData premise2;
        [TextArea] public string verdictProposition;
        public InterrogationData unlocksInterrogation;
    }
}
