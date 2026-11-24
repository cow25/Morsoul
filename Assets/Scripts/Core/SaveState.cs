using System;
using System.Collections.Generic;
using Morsoul.Data;

namespace Morsoul.Core
{
    public enum SceneMode
    {
        Mode3D,
        Mode2D,
        Cinematic,
        Loading,
    }

    public enum PlayerFacing2D
    {
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>
    /// Authoritative save record (GDD §6.8 / Data_Schemas SaveState).
    /// JsonUtility-serializable: Dictionary is flattened to entry lists.
    ///
    /// Version 3 adds scene/player context, Prologue/cutscene state, unique
    /// weighted-board submission histories, per-case grade state, and the
    /// monotonic trusted Focus clock. Version-1 signature files and sealed
    /// version-2 files migrate in SaveSerializer.TryDeserialize.
    /// </summary>
    [Serializable]
    public class SaveState
    {
        public const int CurrentVersion = 4;

        public int saveVersion = CurrentVersion;
        /// <summary>Per-save UUID identity bound into the seal message.</summary>
        public string profileId = Guid.NewGuid().ToString();

        public string playerName = "";
        public int currentCase;
        /// <summary>Physical scene loaded by Continue / mistrial "Retry from Checkpoint".</summary>
        public string checkpointSceneName = "";
        public SceneMode currentSceneMode = SceneMode.Mode2D;
        /// <summary>Stable semantic scene ID; physical scene names are used until route IDs land.</summary>
        public string currentSceneId = "";
        /// <summary>Null until a scene adapter has stamped a resumable pose.</summary>
        public PlayerPositionSave playerPosition;
        public PlayerFacing2D playerFacing2D = PlayerFacing2D.Down;

        public List<string> collectedClueIds = new List<string>();
        /// <summary>Legacy validated node ids retained for compatibility with old saves.</summary>
        public List<string> boardEdges = new List<string>();
        /// <summary>Four slots, one per runic log.</summary>
        public List<bool> runicLogsCollected = new List<bool> { false, false, false, false };
        /// <summary>Display/feedback-only grades for Cases 1–4.</summary>
        public List<Grade> caseGrades = new List<Grade>();
        /// <summary>Backtracking flags; Dictionary flattened for JsonUtility.</summary>
        public List<UneaseFlag> uneaseFlags = new List<UneaseFlag>();
        public List<Tool> toolsUnlocked = new List<Tool>();

        // --- FocusState --------------------------------------------------------
        /// <summary>Current focus energy, 0–focusMax. Persistent; never auto-refills.</summary>
        public float focusEnergy = 100f;
        /// <summary>Focus cap — starts 100, +10 per supported Cases 1–4 complete submission.</summary>
        public float focusMax = 100f;
        /// <summary>Un-credited rest time toward the next Focus tick.</summary>
        public float restElapsedRemainderSeconds;
        /// <summary>Sealed graceful-close timestamp; empty outside a completed close.</summary>
        public string closedAtUtc = "";
        /// <summary>Monotonic highest-seen UTC watermark.</summary>
        public string lastTrustedUtc = "";
        /// <summary>Last offline-recovery credit — prevents double-crediting an interval.</summary>
        public string lastOfflineRecoveryAppliedAtUtc = "";
        /// <summary>Four slots for Cases 1–4; a zero-Focus scan forfeits that case's Clean.</summary>
        public List<bool> strainedScanUsedByCase = new List<bool> { false, false, false, false };

        /// <summary>Per-case weighted board runtime states, keyed by caseId.</summary>
        public List<CaseBoardSave> boardStates = new List<CaseBoardSave>();
        /// <summary>Persistent grading inputs for Cases 1–4.</summary>
        public List<CaseGradeSave> caseGradeStates = new List<CaseGradeSave>();

        // --- Prologue / transition / cinematic state ----------------------------
        public bool prologueCompleted;
        public bool prologueGateUnlocked;
        public bool prologueRobeCollected;
        public bool prologueExitLoaded;
        public bool elizaIntroTriggered;
        public bool elizaIntroSeen;
        public bool easternRouteUnlocked;
        public string activeCutsceneId = "";
        public string preCutsceneCheckpointId = "";
        public ActiveInterrogationSave activeInterrogation;
        public bool hasPlayerPosition;
        public bool hasActiveInterrogation;
        public WorldReturnSave investigationReturn;
        public bool hasInvestigationReturn;
        public int investigationCaseId = -1;
    }

    [Serializable]
    public class WorldReturnSave
    {
        public string sceneName;
        public PlayerPositionSave position = new PlayerPositionSave();
        public PlayerFacing2D facing;
    }

    /// <summary>Persisted player pose; null means no scene-authored pose was saved.</summary>
    [Serializable]
    public class PlayerPositionSave
    {
        public float x;
        public float y;
        public float z;

        public PlayerPositionSave() { }
        public PlayerPositionSave(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
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
        public List<string> submittedWrongConclusionIds = new List<string>();
        public List<string> submittedPartialConclusionIds = new List<string>();
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

    /// <summary>Persistent grading inputs for one graded case.</summary>
    [Serializable]
    public class CaseGradeSave
    {
        public int caseId;
        public int caseErrorCount;
        public List<string> submittedWrongConclusionIds = new List<string>();
        public List<string> submittedPartialConclusionIds = new List<string>();
        public int interrogationStrikeCount;
        public bool mistrialUsed;
        public bool strainedScanUsed;
        public Grade displayGrade = Grade.Ungraded;
    }

    /// <summary>Interrupted interrogation state; null outside an active encounter.</summary>
    [Serializable]
    public class ActiveInterrogationSave
    {
        public int caseId;
        public int phaseIndex;
        public int composureSegmentsRemaining;
        public int strikeCount;
        /// <summary>
        /// Per-case strike count recorded before this encounter began. A
        /// forfeit rewinds grading input to this value so an aborted attempt
        /// does not count its strikes; a mistrial/commit path ignores it.
        /// </summary>
        public int entryStrikeCount;
        public string preInterrogationCheckpointId = "";
    }
}
