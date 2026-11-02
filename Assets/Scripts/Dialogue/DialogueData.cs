using System;
using Morsoul.Data;
using UnityEngine;

namespace Morsoul.Dialogue
{
    [CreateAssetMenu(menuName = "Morsoul/Dialogue")]
    public class DialogueData : ScriptableObject
    {
        public string id;
        public DialogueNodeData[] nodes;
        public string startNodeId;
        /// <summary>Per-speaker portraits; used when a node has no speakerPortrait override.</summary>
        public SpeakerPortraitData[] portraits;
    }

    [Serializable]
    public class SpeakerPortraitData
    {
        public string speakerName;
        public Sprite portrait;
    }

    [Serializable]
    public class DialogueNodeData
    {
        public string id;
        public string speakerName;
        /// <summary>Overrides the speaker's portraits entry for this node only.</summary>
        public Sprite speakerPortrait;
        [TextArea] public string text;
        public ClueData registersClue;
        public string setsFlag;
        /// <summary>Node is skipped if set and the flag is absent.</summary>
        public string requiresFlag;
        /// <summary>0..3 choices; when non-empty, options override nextNodeId.</summary>
        public DialogueOptionData[] options;
        public string nextNodeId;
    }

    [Serializable]
    public class DialogueOptionData
    {
        public string label;
        public string nextNodeId;
        /// <summary>Option is hidden if set and the flag is absent.</summary>
        public string requiresFlag;
    }
}
