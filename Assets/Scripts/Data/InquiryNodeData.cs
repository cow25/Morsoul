using UnityEngine;

namespace Morsoul.Data
{
    [CreateAssetMenu(menuName = "Morsoul/Inquiry Node")]
    public class InquiryNodeData : ScriptableObject
    {
        public string id;
        public ClueData slotA;
        public ClueData slotB;
        public CausalPromptData prompt = new CausalPromptData();
        public PremiseData producesPremise;
        public ClueData[] rejectedClues;
        public string rejectionMessage;
    }
}
