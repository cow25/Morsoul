using System;
using UnityEngine;

namespace Morsoul.Data
{
    [Serializable]
    public class InterrogationPhase
    {
        [TextArea] public string suspectClaim;
        public string[] keywords;
        public MoveData[] validMoves;
        public MoveData[] invalidMoves;
    }
}
