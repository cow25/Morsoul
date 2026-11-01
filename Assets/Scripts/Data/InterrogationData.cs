using UnityEngine;

namespace Morsoul.Data
{
    [CreateAssetMenu(menuName = "Morsoul/Interrogation")]
    public class InterrogationData : ScriptableObject
    {
        public string id;
        public string suspectName;
        public int composureSegments = 3;
        public bool falsehoodShieldActive;
        public InterrogationPhase[] phases;
    }
}
