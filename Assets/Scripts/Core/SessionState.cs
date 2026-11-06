using System;
using System.Collections.Generic;
using Morsoul.Data;
using Morsoul.Engine;

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
        readonly Dictionary<int, BoardRuntimeState> _boardStates = new Dictionary<int, BoardRuntimeState>();
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

        /// <summary>GameFlow's legacy session-wide latent-log latch.</summary>
        public bool LatentLogCollected
        {
            get => HasFlag(LatentLogFlag);
            set => SetFlag(LatentLogFlag, value);
        }

        /// <summary>Flag key tracking that the case's latent log was collected.</summary>
        public static string LatentLogFlagFor(int caseId) => "Session.LatentLog." + caseId;

        /// <summary>Flag key tracking that a mistrial retry penalty applies to the case.</summary>
        public static string MistrialFlagFor(int caseId) => "Session.MistrialRetried." + caseId;

        /// <summary>True when the case's latent log was collected this campaign.</summary>
        public bool IsLatentLogCollected(int caseId) => HasFlag(LatentLogFlagFor(caseId));

        /// <summary>True when the case carried a mistrial retry penalty.</summary>
        public bool WasMistrialRetried(int caseId) => HasFlag(MistrialFlagFor(caseId));

        /// <summary>Flag key tracking a per-case strained scan (forfeits Clean).</summary>
        public static string StrainedScanFlagFor(int caseId) => "Session.StrainedScan." + caseId;

        /// <summary>Marks that a scan ran at 0 Focus during the case.</summary>
        public void MarkStrainedScan(int caseId) => SetFlag(StrainedScanFlagFor(caseId));

        /// <summary>True when a strained scan was used during the case.</summary>
        public bool IsStrainedScanUsed(int caseId) => HasFlag(StrainedScanFlagFor(caseId));

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

        /// <summary>
        /// Per-case weighted-board runtime states (Data_Schemas
        /// BoardRuntimeState). Created on demand; persisted in the save
        /// record and mutated only through Morsoul.Engine.ProgressionRules.
        /// </summary>
        public BoardRuntimeState GetOrCreateBoardState(int caseId)
        {
            if (!_boardStates.TryGetValue(caseId, out var state))
                _boardStates[caseId] = state = new BoardRuntimeState { CaseId = caseId };
            return state;
        }

        public IReadOnlyDictionary<int, BoardRuntimeState> BoardStates => _boardStates;

        /// <summary>Marks runic-log slot (0–3); out-of-range indices ignored.</summary>
        public void MarkRunicLog(int slot)
        {
            if (slot >= 0 && slot < _runicLogs.Length) _runicLogs[slot] = true;
        }

        /// <summary>
        /// Marks runic-log slot by 1-based log id (RunicLogData.id).
        /// </summary>
        public void MarkRunicLogById(int logId) => MarkRunicLog(logId - 1);

        /// <summary>
        /// Registers a collected latent (runic-log) clue: latches the per-case
        /// latent flag and, when the clue id follows the "L&lt;n&gt;_..." naming
        /// convention, marks the matching runic-log slot. Returns true when a
        /// log slot was marked.
        /// </summary>
        public bool RegisterLatentClue(ClueData clue)
        {
            if (clue == null) return false;
            SetFlag(LatentLogFlagFor(clue.caseId));
            LatentLogCollected = true; // keep legacy latch in step
            return TryParseRunicLogSlot(clue.id, out int slot) && Marked(slot);
            bool Marked(int s)
            {
                if (_runicLogs[s]) return false;
                MarkRunicLog(s);
                return true;
            }
        }

        /// <summary>
        /// Parses the runic-log slot (0-3) out of an "L&lt;n&gt;_&lt;case&gt;"
        /// clue id (e.g. L4_04 -> slot 3). False when the id doesn't match.
        /// </summary>
        public static bool TryParseRunicLogSlot(string clueId, out int slot)
        {
            slot = -1;
            if (string.IsNullOrEmpty(clueId) || clueId[0] != 'L' || clueId.Length < 2
                || !char.IsDigit(clueId[1]))
                return false;
            int digits = 0;
            int num = 0;
            while (1 + digits < clueId.Length && char.IsDigit(clueId[1 + digits]))
            {
                num = num * 10 + (clueId[1 + digits] - '0');
                digits++;
            }
            if (digits == 0 || num < 1 || num > 4) return false;
            slot = num - 1;
            return true;
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
            save.boardStates = new List<CaseBoardSave>();
            foreach (var pair in _boardStates)
                save.boardStates.Add(ToSave(pair.Value));
        }

        static CaseBoardSave ToSave(BoardRuntimeState s)
        {
            var save = new CaseBoardSave
            {
                caseId = s.CaseId,
                visibleClueIds = new List<string>(s.VisibleClueIds),
                collectedClueIds = new List<string>(s.CollectedClueIds),
                selectedConclusionId = s.SelectedConclusionId ?? "",
                caseErrorCount = s.CaseErrorCount,
                firedHintTiers = new List<int>(s.FiredHintTiers),
                linkingLockedOut = s.LinkingLockedOut,
                submittedConclusionId = s.SubmittedConclusionId ?? "",
                boardSolved = s.BoardSolved,
                completionRewardApplied = s.CompletionRewardApplied,
            };
            foreach (var link in s.Links)
                save.links.Add(new BoardLinkSave(link.sourceId, link.targetId));
            foreach (var pair in s.NodePositions)
                save.nodePositions.Add(new NodePositionSave(pair.Key, pair.Value.x, pair.Value.y));
            return save;
        }

        static void FromSave(CaseBoardSave save, BoardRuntimeState s)
        {
            s.CaseId = save.caseId;
            s.VisibleClueIds = new List<string>(save.visibleClueIds ?? new List<string>());
            s.CollectedClueIds = new List<string>(save.collectedClueIds ?? new List<string>());
            s.Links = new List<DirectedEdge>();
            foreach (var l in save.links ?? new List<BoardLinkSave>())
                if (l != null)
                    s.Links.Add(new DirectedEdge(l.sourceId, l.targetId));
            s.NodePositions = new Dictionary<string, NodePosition>();
            foreach (var p in save.nodePositions ?? new List<NodePositionSave>())
                if (p != null)
                    s.NodePositions[p.clueId] = new NodePosition { x = p.x, y = p.y };
            s.SelectedConclusionId = save.selectedConclusionId;
            s.CaseErrorCount = save.caseErrorCount;
            s.FiredHintTiers = new List<int>(save.firedHintTiers ?? new List<int>());
            s.LinkingLockedOut = save.linkingLockedOut;
            s.SubmittedConclusionId = save.submittedConclusionId;
            s.BoardSolved = save.boardSolved;
            s.CompletionRewardApplied = save.completionRewardApplied;
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
            foreach (var boardSave in save.boardStates ?? new List<CaseBoardSave>())
                if (boardSave != null)
                {
                    var s = GetOrCreateBoardState(boardSave.caseId);
                    FromSave(boardSave, s);
                }
            // v1 migration: the global strained-scan flag maps to the current case.
            if (save.strainedScanUsed)
                MarkStrainedScan(save.currentCase);
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
            _boardStates.Clear();
            Array.Clear(_runicLogs, 0, _runicLogs.Length);
            _grades.Clear();
            _tools.Clear();
        }
    }
}
