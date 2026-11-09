using System;
using System.Collections.Generic;
using Morsoul.Data;
using Morsoul.Engine;
using UnityEngine;

namespace Morsoul.Core
{
    public enum InvestigationCompletionSource { Unknown, Real, Simulated }

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
        /// <summary>Dedicated post-ruins 2D approach; not the Case 3 Whisperwood hub.</summary>
        public const string PrologueWildernessSceneId = "Prologue_Wilderness";
        /// <summary>Active cutscene id used while the in-world Eliza introduction runs.</summary>
        public const string ElizaIntroCutsceneId = "CS00_ELIZA_INTRO";
        /// <summary>Checkpoint id captured before the first-contact trigger mutates state.</summary>
        public const string ElizaPreContactCheckpointId = "case00_eliza_precontact";

        public string PlayerName = "";
        public int CurrentCase;
        public SceneMode CurrentSceneMode = SceneMode.Mode2D;
        public string CurrentSceneId = "";
        public Vector3 PlayerPosition = Vector3.zero;
        public bool HasPlayerPosition;
        public PlayerFacing2D PlayerFacing2D = PlayerFacing2D.Down;

        public bool PrologueCompleted;
        public bool PrologueGateUnlocked;
        public bool PrologueRobeCollected;
        public bool PrologueExitLoaded;
        public bool ElizaIntroTriggered;
        public bool ElizaIntroSeen;
        public bool EasternRouteUnlocked;
        public string ActiveCutsceneId = "";
        public string PreCutsceneCheckpointId = "";
        /// <summary>
        /// Runtime-only handoff set by exits that name a destination spawn
        /// marker (GateInteractable.entryId). Consumed by SceneEntryPoint on
        /// arrival; never serialized, so checkpoints fall back to pose/spawn.
        /// </summary>
        public string PendingEntryPointId = "";
        public ActiveInterrogationSave ActiveInterrogation;
        public WorldReturnSave InvestigationReturn;
        public int InvestigationCaseId = -1;

        public void CaptureInvestigationReturn()
        {
            InvestigationReturn = CurrentSceneMode == SceneMode.Mode2D && HasPlayerPosition &&
                                  !string.IsNullOrEmpty(CurrentSceneId)
                ? new WorldReturnSave
                {
                    sceneName = CurrentSceneId,
                    position = new PlayerPositionSave(PlayerPosition.x, PlayerPosition.y, PlayerPosition.z),
                    facing = PlayerFacing2D,
                } : null;
        }

        static WorldReturnSave Clone(WorldReturnSave source) => source == null ? null : new WorldReturnSave
        {
            sceneName = source.sceneName,
            position = source.position == null ? new PlayerPositionSave() :
                new PlayerPositionSave(source.position.x, source.position.y, source.position.z),
            facing = source.facing,
        };

        readonly HashSet<string> _clueIds = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, ClueData> _clueAssets = new Dictionary<string, ClueData>(StringComparer.Ordinal);
        readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>(StringComparer.Ordinal);
        readonly List<string> _boardEdges = new List<string>();
        readonly Dictionary<int, BoardRuntimeState> _boardStates = new Dictionary<int, BoardRuntimeState>();
        readonly bool[] _runicLogs = new bool[4];
        readonly Dictionary<int, Grade> _grades = new Dictionary<int, Grade>();
        readonly Dictionary<int, int> _interrogationStrikes = new Dictionary<int, int>();
        readonly Dictionary<int, CaseGradeSave> _caseGradeStates =
            new Dictionary<int, CaseGradeSave>();
        readonly HashSet<Tool> _tools = new HashSet<Tool>();

        /// <summary>Updates the persistent scene/player context without touching progression.</summary>
        public void SetPlayerContext(SceneMode mode, string sceneId,
            Vector3 position, PlayerFacing2D facing)
        {
            CurrentSceneMode = mode;
            CurrentSceneId = sceneId ?? "";
            PlayerPosition = position;
            HasPlayerPosition = true;
            PlayerFacing2D = facing;
        }

        /// <summary>Moves to a scene without a resumable pose; entry points will stamp one.</summary>
        public void SetSceneContext(SceneMode mode, string sceneId)
        {
            CurrentSceneMode = mode;
            CurrentSceneId = sceneId ?? "";
            PlayerPosition = Vector3.zero;
            HasPlayerPosition = false;
        }

        /// <summary>Updates cutscene checkpoint context; empty IDs clear the active event.</summary>
        public void SetCutsceneContext(string activeCutsceneId, string preCutsceneCheckpointId)
        {
            ActiveCutsceneId = activeCutsceneId ?? "";
            PreCutsceneCheckpointId = preCutsceneCheckpointId ?? "";
        }

        /// <summary>Stores an interrupted interrogation or clears it with null.</summary>
        public void SetActiveInterrogation(ActiveInterrogationSave state)
        {
            ActiveInterrogation = state;
        }

        /// <summary>
        /// Rewinds an interrupted Eliza first-contact save to its pre-trigger
        /// state. The player resumes west of the trigger so the scene can fire
        /// once rather than being skipped or resumed halfway through.
        /// </summary>
        public bool ResumeInterruptedElizaIntro()
        {
            if (ActiveCutsceneId != ElizaIntroCutsceneId ||
                ElizaIntroSeen || !ElizaIntroTriggered)
                return false;
            ElizaIntroTriggered = false;
            EasternRouteUnlocked = false;
            PrologueExitLoaded = true;
            SetCutsceneContext("", "");
            // Must stay west of the ElizaIntroTrigger's west edge (the
            // controller keeps that edge at x<=3.6 via triggerMaxX) so the
            // player can walk east and re-trigger the encounter.
            SetPlayerContext(SceneMode.Mode2D, PrologueWildernessSceneId,
                new Vector3(1.5f, 0f, 0f), PlayerFacing2D.Right);
            return true;
        }

        /// <summary>Records the persisted interrogation strike total for a graded case.</summary>
        public void SetInterrogationStrikeCount(int caseId, int strikeCount)
        {
            if (caseId >= 1 && caseId <= 4)
                _interrogationStrikes[caseId] = Mathf.Clamp(strikeCount, 0, 3);
        }

        public int GetInterrogationStrikeCount(int caseId) =>
            _interrogationStrikes.TryGetValue(caseId, out int count) ? count : 0;

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

        public bool CanStartInterrogation(int caseId) =>
            caseId >= 1 && caseId <= 4 && CurrentCase == caseId &&
            _boardStates.TryGetValue(caseId, out var board) && board.BoardSolved &&
            (!_grades.TryGetValue(caseId, out var grade) || grade == Grade.Ungraded);

        public BoardRuntimeState CopyBoardState(int caseId)
        {
            var copy = new BoardRuntimeState { CaseId = caseId };
            if (_boardStates.TryGetValue(caseId, out var state))
            {
                FromSave(ToSave(state), copy);
                copy.Focus = state.Focus;
                copy.MaxFocus = state.MaxFocus;
            }
            return copy;
        }

        public bool AcceptInvestigation(BoardRuntimeState accepted,
            InvestigationCompletionSource source)
        {
            if (accepted == null || !accepted.BoardSolved || accepted.CaseId < 0 || accepted.CaseId > 4)
                return false;
            if (_boardStates.TryGetValue(accepted.CaseId, out var existing) && existing.BoardSolved)
                return false;
            var copy = new BoardRuntimeState();
            FromSave(ToSave(accepted), copy);
            copy.Focus = accepted.Focus;
            copy.MaxFocus = accepted.MaxFocus;
            _boardStates[accepted.CaseId] = copy;
            RecordInvestigationSource(accepted.CaseId, source);
            return true;
        }

        public InvestigationCompletionSource GetInvestigationSource(int caseId) =>
            HasFlag("Investigation.Source.Real." + caseId) ? InvestigationCompletionSource.Real :
            HasFlag("Investigation.Source.Simulated." + caseId) ? InvestigationCompletionSource.Simulated :
            InvestigationCompletionSource.Unknown;

        public void RecordInvestigationSource(int caseId, InvestigationCompletionSource source)
        {
            if (GetInvestigationSource(caseId) != InvestigationCompletionSource.Unknown ||
                source == InvestigationCompletionSource.Unknown) return;
            SetFlag("Investigation.Source." + source + "." + caseId);
        }

        public bool WasInvestigationSimulated(int caseId) => HasFlag("Investigation.SimulationUsed." + caseId);
        public bool WasClueSimulated(int caseId, string clueId) =>
            HasFlag("Investigation.SimulatedClue." + caseId + "." + clueId);

        public void RecordInvestigationSimulation(int caseId, IEnumerable<string> clueIds)
        {
            SetFlag("Investigation.SimulationUsed." + caseId);
            if (clueIds == null) return;
            foreach (string id in clueIds)
                if (!string.IsNullOrEmpty(id)) SetFlag("Investigation.SimulatedClue." + caseId + "." + id);
        }

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
            save.currentSceneMode = CurrentSceneMode;
            save.currentSceneId = CurrentSceneId;
            save.playerPosition = HasPlayerPosition
                ? new PlayerPositionSave(PlayerPosition.x, PlayerPosition.y, PlayerPosition.z)
                : null;
            save.playerFacing2D = PlayerFacing2D;
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
            save.strainedScanUsedByCase = new List<bool>();
            for (int i = 1; i <= 4; i++)
                save.strainedScanUsedByCase.Add(IsStrainedScanUsed(i));
            save.boardStates = new List<CaseBoardSave>();
            foreach (var pair in _boardStates)
                save.boardStates.Add(ToSave(pair.Value));
            save.caseGradeStates = new List<CaseGradeSave>();
            for (int i = 1; i <= 4; i++)
            {
                _boardStates.TryGetValue(i, out BoardRuntimeState board);
                _caseGradeStates.TryGetValue(i, out CaseGradeSave persisted);
                save.caseGradeStates.Add(new CaseGradeSave
                {
                    caseId = i,
                    caseErrorCount = board?.CaseErrorCount ??
                                     persisted?.caseErrorCount ?? 0,
                    submittedWrongConclusionIds = board != null
                        ? new List<string>(board.SubmittedWrongConclusionIds)
                        : new List<string>(persisted?.submittedWrongConclusionIds ??
                                           new List<string>()),
                    submittedPartialConclusionIds = board != null
                        ? new List<string>(board.SubmittedPartialConclusionIds)
                        : new List<string>(persisted?.submittedPartialConclusionIds ??
                                           new List<string>()),
                    interrogationStrikeCount = _interrogationStrikes.TryGetValue(i, out int strikes)
                        ? strikes
                        : persisted?.interrogationStrikeCount ?? 0,
                    mistrialUsed = WasMistrialRetried(i) ||
                                   (persisted?.mistrialUsed ?? false),
                    strainedScanUsed = IsStrainedScanUsed(i) ||
                                       (persisted?.strainedScanUsed ?? false),
                    displayGrade = save.caseGrades[i - 1],
                });
            }
            save.prologueCompleted = PrologueCompleted;
            save.prologueGateUnlocked = PrologueGateUnlocked;
            save.prologueRobeCollected = PrologueRobeCollected;
            save.prologueExitLoaded = PrologueExitLoaded;
            save.elizaIntroTriggered = ElizaIntroTriggered;
            save.elizaIntroSeen = ElizaIntroSeen;
            save.easternRouteUnlocked = EasternRouteUnlocked;
            save.activeCutsceneId = ActiveCutsceneId;
            save.preCutsceneCheckpointId = PreCutsceneCheckpointId;
            save.activeInterrogation = Clone(ActiveInterrogation);
            save.investigationReturn = Clone(InvestigationReturn);
            save.investigationCaseId = InvestigationCaseId;
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
                submittedWrongConclusionIds = new List<string>(s.SubmittedWrongConclusionIds),
                submittedPartialConclusionIds = new List<string>(s.SubmittedPartialConclusionIds),
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
            s.SubmittedWrongConclusionIds = new List<string>(
                save.submittedWrongConclusionIds ?? new List<string>());
            s.SubmittedPartialConclusionIds = new List<string>(
                save.submittedPartialConclusionIds ?? new List<string>());
            s.BoardSolved = save.boardSolved;
            s.CompletionRewardApplied = save.completionRewardApplied;
        }

        static CaseGradeSave Clone(CaseGradeSave source)
        {
            if (source == null) return null;
            return new CaseGradeSave
            {
                caseId = source.caseId,
                caseErrorCount = source.caseErrorCount,
                submittedWrongConclusionIds = new List<string>(
                    source.submittedWrongConclusionIds ?? new List<string>()),
                submittedPartialConclusionIds = new List<string>(
                    source.submittedPartialConclusionIds ?? new List<string>()),
                interrogationStrikeCount = source.interrogationStrikeCount,
                mistrialUsed = source.mistrialUsed,
                strainedScanUsed = source.strainedScanUsed,
                displayGrade = source.displayGrade,
            };
        }

        static ActiveInterrogationSave Clone(ActiveInterrogationSave source)
        {
            if (source == null) return null;
            return new ActiveInterrogationSave
            {
                caseId = source.caseId,
                phaseIndex = source.phaseIndex,
                composureSegmentsRemaining = source.composureSegmentsRemaining,
                strikeCount = source.strikeCount,
                entryStrikeCount = source.entryStrikeCount,
                preInterrogationCheckpointId = source.preInterrogationCheckpointId ?? "",
            };
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
            CurrentSceneMode = save.currentSceneMode;
            CurrentSceneId = save.currentSceneId ?? "";
            HasPlayerPosition = save.playerPosition != null;
            PlayerPosition = save.playerPosition != null
                ? new Vector3(save.playerPosition.x, save.playerPosition.y, save.playerPosition.z)
                : Vector3.zero;
            PlayerFacing2D = save.playerFacing2D;
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
            if (save.strainedScanUsedByCase != null)
                for (int i = 0; i < save.strainedScanUsedByCase.Count && i < 4; i++)
                    if (save.strainedScanUsedByCase[i])
                        MarkStrainedScan(i + 1);
            foreach (var boardSave in save.boardStates ?? new List<CaseBoardSave>())
                if (boardSave != null)
                {
                    var s = GetOrCreateBoardState(boardSave.caseId);
                    FromSave(boardSave, s);
                }
            foreach (var gradeSave in save.caseGradeStates ?? new List<CaseGradeSave>())
                if (gradeSave != null && gradeSave.caseId >= 1 && gradeSave.caseId <= 4)
                {
                    _caseGradeStates[gradeSave.caseId] = Clone(gradeSave);
                    SetInterrogationStrikeCount(
                        gradeSave.caseId, gradeSave.interrogationStrikeCount);
                    if (gradeSave.mistrialUsed)
                        SetFlag(MistrialFlagFor(gradeSave.caseId));
                    if (gradeSave.strainedScanUsed)
                        MarkStrainedScan(gradeSave.caseId);
                    if (gradeSave.displayGrade != Grade.Ungraded)
                        SetGrade(gradeSave.caseId, gradeSave.displayGrade);
                }
            PrologueCompleted = save.prologueCompleted;
            PrologueGateUnlocked = save.prologueGateUnlocked;
            PrologueRobeCollected = save.prologueRobeCollected;
            PrologueExitLoaded = save.prologueExitLoaded;
            ElizaIntroTriggered = save.elizaIntroTriggered;
            ElizaIntroSeen = save.elizaIntroSeen;
            EasternRouteUnlocked = save.easternRouteUnlocked;
            ActiveCutsceneId = save.activeCutsceneId ?? "";
            PreCutsceneCheckpointId = save.preCutsceneCheckpointId ?? "";
            ActiveInterrogation = Clone(save.activeInterrogation);
            InvestigationReturn = Clone(save.investigationReturn);
            InvestigationCaseId = save.investigationCaseId;
        }

        /// <summary>Clears everything (new game).</summary>
        public void Reset()
        {
            PlayerName = "";
            CurrentCase = 0;
            CurrentSceneMode = SceneMode.Mode2D;
            CurrentSceneId = "";
            PlayerPosition = Vector3.zero;
            HasPlayerPosition = false;
            PlayerFacing2D = PlayerFacing2D.Down;
            PrologueCompleted = false;
            PrologueGateUnlocked = false;
            PrologueRobeCollected = false;
            PrologueExitLoaded = false;
            ElizaIntroTriggered = false;
            ElizaIntroSeen = false;
            EasternRouteUnlocked = false;
            ActiveCutsceneId = "";
            PreCutsceneCheckpointId = "";
            PendingEntryPointId = "";
            ActiveInterrogation = null;
            InvestigationReturn = null;
            InvestigationCaseId = -1;
            _clueIds.Clear();
            _clueAssets.Clear();
            _flags.Clear();
            _boardEdges.Clear();
            _boardStates.Clear();
            Array.Clear(_runicLogs, 0, _runicLogs.Length);
            _grades.Clear();
            _interrogationStrikes.Clear();
            _caseGradeStates.Clear();
            _tools.Clear();
        }
    }
}
