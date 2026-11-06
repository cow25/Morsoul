using System;
using System.Collections.Generic;
using Morsoul.Data;

namespace Morsoul.Core
{
    /// <summary>
    /// Authoritative save record (GDD §6.8 / Data_Schemas SaveState).
    /// JsonUtility-serializable: Dictionary is flattened to entry lists.
    ///
    /// Version 2 adds the weighted-board FocusState fields and per-case
    /// BoardRuntimeState records. Version-1 files (focusPercent, validated
    /// node ids, global strainedScanUsed) migrate on load in
    /// SaveSerializer.TryDeserialize.
    /// </summary>
    [Serializable]
    public class SaveState
    {
        public const int CurrentVersion = 2;

        public int saveVersion = CurrentVersion;
        /// <summary>Identity bound into the seal message; "local" until profiles exist.</summary>
        public string profileId = "local";

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

        // --- FocusState (v2) --------------------------------------------------
        /// <summary>Current focus energy, 0–focusMax. Persistent; never auto-refills.</summary>
        public float focusEnergy = 100f;
        /// <summary>Focus cap — starts 100, +10 per supported complete submission.</summary>
        public float focusMax = 100f;
        /// <summary>Un-credited rest time toward the next whole minute.</summary>
        public float restElapsedRemainderSeconds;
        /// <summary>ISO-8601 close timestamp for offline recovery; empty when consumed.</summary>
        public string closedAtUtc = "";
        /// <summary>Last offline-recovery credit — prevents double-crediting an interval.</summary>
        public string lastOfflineRecoveryAppliedAtUtc = "";
        /// <summary>True if a strained scan ran at 0 — v1 global field; per-case flags supersede it.</summary>
        public bool strainedScanUsed;

        /// <summary>Per-case weighted board runtime states, keyed by caseId.</summary>
        public List<CaseBoardSave> boardStates = new List<CaseBoardSave>();

        // --- Legacy v1 fields (read on migration; no longer written) ----------
        /// <summary>v1 0–100 focus percent; migrated into focusEnergy on load.</summary>
        public float focusPercent = 100f;
    }

    /// <summary>Serializable pair for one drawn directed link.</summary>
    [Serializable]
    public class BoardLinkSave
    {
        public string sourceId;
        public string targetId;

        public BoardLinkSave() { }
        public BoardLinkSave(string sourceId, string targetId)
        {
            this.sourceId = sourceId;
            this.targetId = targetId;
        }
    }

    /// <summary>Serializable board node position entry.</summary>
    [Serializable]
    public class NodePositionSave
    {
        public string clueId;
        public float x;
        public float y;

        public NodePositionSave() { }
        public NodePositionSave(string clueId, float x, float y)
        {
            this.clueId = clueId;
            this.x = x;
            this.y = y;
        }
    }

    /// <summary>Per-case BoardRuntimeState record (Data_Schemas).</summary>
    [Serializable]
    public class CaseBoardSave
    {
        public int caseId;
        public List<string> visibleClueIds = new List<string>();
        public List<string> collectedClueIds = new List<string>();
        public List<BoardLinkSave> links = new List<BoardLinkSave>();
        public List<NodePositionSave> nodePositions = new List<NodePositionSave>();
        public string selectedConclusionId = "";
        public int caseErrorCount;
        public List<int> firedHintTiers = new List<int>();
        public bool linkingLockedOut;
        public string submittedConclusionId = "";
        public bool boardSolved;
        public bool completionRewardApplied;
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
