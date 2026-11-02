using System;
using System.Collections.Generic;
using Morsoul.Data;

namespace Morsoul.Core
{
    /// <summary>
    /// Authoritative save record (GDD §6.8 / Data_Schemas SaveState).
    /// JsonUtility-serializable: Dictionary is flattened to entry lists.
    /// </summary>
    [Serializable]
    public class SaveState
    {
        public string playerName = "";
        public int currentCase;
        /// <summary>Scene to load on Continue / mistrial "Retry from Checkpoint".</summary>
        public string checkpointSceneName = "";
        public List<string> collectedClueIds = new List<string>();
        /// <summary>Validated board node ids (DeductionBoard.ValidatedNodeIds).</summary>
        public List<string> boardEdges = new List<string>();
        /// <summary>Four slots, one per runic log.</summary>
        public List<bool> runicLogsCollected = new List<bool> { false, false, false, false };
        /// <summary>One grade per graded case (1–4).</summary>
        public List<Grade> caseGrades = new List<Grade>();
        /// <summary>Backtracking flags; Dictionary flattened for JsonUtility.</summary>
        public List<UneaseFlag> uneaseFlags = new List<UneaseFlag>();
        public List<Tool> toolsUnlocked = new List<Tool>();
        /// <summary>0–100 persistent Focus; never auto-refills.</summary>
        public float focusPercent = 100f;
        /// <summary>True if a strained scan ran at 0% — forfeits Clean.</summary>
        public bool strainedScanUsed;
    }

    [Serializable]
    public class UneaseFlag
    {
        public string key;
        public bool value;

        public UneaseFlag() { }
        public UneaseFlag(string key, bool value)
        {
            this.key = key;
            this.value = value;
        }
    }
}
