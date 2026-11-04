using System;
using System.Collections.Generic;
using Morsoul.Data;

namespace Morsoul.Core
{
    /// <summary>
    /// Live cross-scene session truth. Scene-scoped stores (HubJournal,
    /// HubFlags) sync against this so progress survives scene loads;
    /// GameStateManager flattens it into <see cref="SaveState"/> on every
    /// checkpoint and restores it on Continue.
    ///
    /// Clue truth is the id set; <see cref="ClueAssets"/> is a runtime cache
    /// of the ScriptableObjects seen this run so scene UI can rehydrate by
    /// id without asset-database lookups. After a Continue from a fresh
    /// launch the cache starts empty — ids persist, and clue assets repopulate
    /// as scenes re-register them (journal may show fewer cards until then;
    /// known limitation).
    /// </summary>
    public class SessionState
    {
        /// <summary>Reserved flag key carrying GameFlow's latentLogCollected.</summary>
        public const string LatentLogFlag = "Session.LatentLogCollected";

        public string PlayerName = "";
        public int CurrentCase;

        readonly HashSet<string> _clueIds = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, ClueData> _clueAssets = new Dictionary<string, ClueData>(StringComparer.Ordinal);
        readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>(StringComparer.Ordinal);
        readonly List<string> _boardEdges = new List<string>();
        readonly bool[] _runicLogs = new bool[4];
        readonly Dictionary<int, Grade> _grades = new Dictionary<int, Grade>();
        readonly HashSet<Tool> _tools = new HashSet<Tool>();

        // --- Clues ----------------------------------------------------------

        /// <summary>Registers a clue; returns true when the id is new.</summary>
        public bool RegisterClue(ClueData clue)
        {
            if (clue == null || string.IsNullOrEmpty(clue.id)) return false;
            _clueAssets[clue.id] = clue;
            return _clueIds.Add(clue.id);
        }

        /// <summary>Records a clue id without an asset (save restore).</summary>
        public bool RegisterClueId(string clueId)
        {
            return !string.IsNullOrEmpty(clueId) && _clueIds.Add(clueId);
        }

        public IReadOnlyCollection<string> ClueIds => _clueIds;
        public IReadOnlyDictionary<string, ClueData> ClueAssets => _clueAssets;
        public bool HasClue(string clueId) =>
            !string.IsNullOrEmpty(clueId) && _clueIds.Contains(clueId);

        // --- Flags ------------------------------------------------------------

        /// <summary>Sets a flag's value (presence-style flags pass true).</summary>
        public void SetFlag(string key, bool value = true)
        {
            if (!string.IsNullOrEmpty(key)) _flags[key] = value;
        }

        public bool HasFlag(string key) =>
            !string.IsNullOrEmpty(key) && _flags.TryGetValue(key, out bool v) && v;

        public IReadOnlyDictionary<string, bool> Flags => _flags;

        /// <summary>GameFlow's per-case latent-log latch, carried as a reserved flag.</summary>
        public bool LatentLogCollected
        {
            get => HasFlag(LatentLogFlag);
            set => SetFlag(LatentLogFlag, value);
        }

        // --- Board / logs / grades / tools ------------------------------------

        /// <summary>Merges validated board node ids (DeductionBoard.ValidatedNodeIds).</summary>
        public void MergeBoardEdges(IEnumerable<string> nodeIds)
        {
            if (nodeIds == null) return;
            foreach (var id in nodeIds)
                if (!string.IsNullOrEmpty(id) && !_boardEdges.Contains(id))
                    _boardEdges.Add(id);
        }

        public IReadOnlyList<string> BoardEdges => _boardEdges;

        /// <summary>Marks runic-log slot (0–3); out-of-range indices ignored.</summary>
        public void MarkRunicLog(int slot)
        {
            if (slot >= 0 && slot < _runicLogs.Length) _runicLogs[slot] = true;
        }

        public IReadOnlyList<bool> RunicLogs => _runicLogs;
        public int RunicLogCount => Array.FindAll(_runicLogs, b => b).Length;

        public void SetGrade(int caseId, Grade grade)
        {
            if (caseId > 0) _grades[caseId] = grade;
        }

        public IReadOnlyDictionary<int, Grade> Grades => _grades;

        public void UnlockTool(Tool tool) => _tools.Add(tool);
        public IReadOnlyCollection<Tool> Tools => _tools;

        // --- Save bridge --------------------------------------------------------

        /// <summary>Flattens session state into the save record.</summary>
        public void ApplyTo(SaveState save)
        {
            if (save == null) return;
            save.playerName = PlayerName;
            save.currentCase = CurrentCase;
            save.collectedClueIds = new List<string>(_clueIds);
            save.boardEdges = new List<string>(_boardEdges);
            save.runicLogsCollected = new List<bool>(_runicLogs);
            save.caseGrades = new List<Grade>();
            for (int i = 1; i <= 4; i++)
                save.caseGrades.Add(_grades.TryGetValue(i, out Grade g) ? g : Grade.Ungraded);
            save.uneaseFlags = new List<UneaseFlag>();
            foreach (var kv in _flags)
                save.uneaseFlags.Add(new UneaseFlag(kv.Key, kv.Value));
            save.toolsUnlocked = new List<Tool>(_tools);
        }

        /// <summary>
        /// Restores session state from a loaded save record. When
        /// <paramref name="clueResolver"/> is provided, saved ids are resolved
        /// back to ClueData assets (ClueRegistry) so scene UI rehydrates;
        /// unresolved ids are still tracked.
        /// </summary>
        public void RestoreFrom(SaveState save, Func<string, ClueData> clueResolver = null)
        {
            if (save == null) return;
            Reset();
            PlayerName = save.playerName ?? "";
            CurrentCase = save.currentCase;
            if (save.collectedClueIds != null)
                foreach (var id in save.collectedClueIds)
                {
                    var clue = clueResolver?.Invoke(id);
                    if (clue != null) RegisterClue(clue);
                    else RegisterClueId(id);
                }
            MergeBoardEdges(save.boardEdges);
            if (save.runicLogsCollected != null)
                for (int i = 0; i < _runicLogs.Length && i < save.runicLogsCollected.Count; i++)
                    _runicLogs[i] = save.runicLogsCollected[i];
            if (save.caseGrades != null)
                for (int i = 0; i < save.caseGrades.Count; i++)
                    SetGrade(i + 1, save.caseGrades[i]);
            if (save.uneaseFlags != null)
                foreach (var f in save.uneaseFlags)
                    if (f != null) SetFlag(f.key, f.value);
            if (save.toolsUnlocked != null)
                foreach (var t in save.toolsUnlocked) UnlockTool(t);
        }

        /// <summary>Clears everything (new game).</summary>
        public void Reset()
        {
            PlayerName = "";
            CurrentCase = 0;
            _clueIds.Clear();
            _clueAssets.Clear();
            _flags.Clear();
            _boardEdges.Clear();
            Array.Clear(_runicLogs, 0, _runicLogs.Length);
            _grades.Clear();
            _tools.Clear();
        }
    }
}
