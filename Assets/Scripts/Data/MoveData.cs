using System;
using UnityEngine;

namespace Morsoul.Data
{
    [Serializable]
    public class MoveData
    {
        public Verb verb;
        public EvidenceData evidence;
        public bool breaksComposureSegment;
        public int strikeCost;
        public bool isCoreContradiction;
        [TextArea] public string responseDialogue;
        public Aura emotionAura;
    }
}
