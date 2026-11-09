using System;
using System.Collections.Generic;
using System.Linq;
using Morsoul.Board;
using Morsoul.Cutscene;
using Morsoul.Data;
using Morsoul.Dialogue;
using Morsoul.Gameplay;
using Morsoul.Hub;
using Morsoul.Interrogation;
using Morsoul.Journal;
using Morsoul.Resolution;
using UnityEngine;

namespace Morsoul.Flow
{
    /// <summary>
    /// Bootstrap-scene glue that binds the panel controllers into the case loop:
    /// board -> interrogation -> resolution, plus ending routing and grade
    /// tracking through <see cref="CaseSession"/>. All event handlers are public
    /// methods so EditMode tests can drive the flow without a scene or UI.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        [Header("Case")]
        public CaseData activeCase;
        public bool investigationScene;
        [Tooltip("Optional override used to resolve Session.CurrentCase when no " +
                 "activeCase is assigned; falls back to the generated " +
                 "Resources/CaseDatabase via CoreRuntime.ResolveCase.")]
        public CaseDatabase caseDatabase;
        [Tooltip("Fallback runic-log count used only when no CoreRuntime session exists.")]
        public int runicLogsCollected;
        [Tooltip("True once the active case's latent log clue has been collected.")]
        public bool latentLogCollected;

        [Header("Endings")]
        [Tooltip("Cutscene per ending (index = ending number, 1-3); played before case completion routes.")]
        public CutsceneData[] endingCutscenes = new CutsceneData[4];
        [Tooltip("Optional. Played once per save when a case without an interrogation " +
                 "opens (e.g. the Case 5 'Echoes of the Past' entry cinematic); the " +
                 "board waits for it to finish. Unassigned opens the board directly.")]
        public CutsceneData entryCutscene;

        [Header("Panel controllers")]
        public BoardController board;
        [Tooltip("Weighted evidence-DAG board (Algorithm.md). Used when the case " +
                 "has an EvidenceGraph; the legacy board handles slot/premise cases.")]
        public WeightedBoardController weightedBoard;
        public InterrogationController interrogation;
        public ResolutionController resolution;
        [Tooltip("Plays the ending cutscene selected by the case loop.")]
        public CutscenePlayerController cutscenePlayer;
        [Tooltip("Optional. Clues registered by dialogue feed the journal + evidence.")]
        public DialogueController dialogue;
        [Tooltip("Optional. Hub-scene clue store; absent outside hub scenes.")]
        public HubJournal hubJournal;
        [Tooltip("Optional. Journal overlay; gated so it cannot open over the " +
                 "board, interrogation, resolution or an ending cutscene.")]
        public JournalController journalPanel;
        [Tooltip("Optional. Dialogue flags flow into this set + the Core session.")]
        public HubFlags flags;

        /// <summary>Raised when the run should route to an ending (1 = board fail, 2/3 = EndingSelector).</summary>
        public event Action<int> EndingRouteRequested;
        /// <summary>Raised when the player confirms "Retry from Checkpoint"; the GSM reload consumes it.</summary>
        public event Action MistrialRetryRequested;
        /// <summary>Re-emitted Focus costs for the (future) FocusModel. Second arg is the source.</summary>
        public event Action<float, string> FocusSpent;
        /// <summary>Re-emitted when dialogue registers a clue.</summary>
        public event Action<ClueData> ClueRegistered;

        CaseSession _session;
        readonly List<ClueData> _collectedClues = new List<ClueData>();
        bool _endingRequested;
        bool _endingCompleted;
        bool _mistrialHandled;
        bool _entryCutscenePending;
        int _lastEnding;
        int _reportedStrikes;

        public CaseSession Session => _session;
        /// <summary>Grade of the current session (Ungraded before BeginCase) — used by save.</summary>
        public Grade CurrentGrade
        {
            get
            {
                SyncBoardGradeInputs();
                return _session != null ? _session.ComputeGrade() : Grade.Ungraded;
            }
        }

        void OnEnable()
        {
            Bind();
            if (activeCase != null) BeginCase(activeCase);
            else if (Application.isPlaying) TryBeginSessionCase();
        }

        /// <summary>
        /// Resolves the case to play from the Core session (CurrentCase) through
        /// the case database — the campaign entry used by the shared CaseLoop
        /// scene when no activeCase is assigned. False (with a warning) when
        /// nothing resolves.
        /// </summary>
        public bool TryBeginSessionCase()
        {
            var core = Core.CoreRuntime.Ensure();
            int caseId = investigationScene && core.Session.InvestigationCaseId >= 0
                ? core.Session.InvestigationCaseId : core.Session.CurrentCase;
            var data = caseDatabase != null
                ? caseDatabase.Find(caseId)
                : core.ResolveCase(caseId);
            if (data == null)
            {
                Debug.LogWarning($"GameFlow: no CaseData for session case {core.Session.CurrentCase} " +
                                 "(rebuild the case database).", this);
                return false;
            }
            BeginCase(data);
            return true;
        }

        void OnDisable() => Unbind();

        /// <summary>(Re)subscribes to the wired controllers. Idempotent.</summary>
        public void Bind()
        {
            Unbind();
            if (board != null)
            {
                board.FocusCosted += HandleBoardFocusCosted;
                board.StrikeCosted += HandleBoardStrikeCosted;
                board.InterrogationUnlocked += HandleInterrogationUnlocked;
                board.HypothesisRoutesToEnding += HandleRoutesToEnding;
                board.AnswerResolved += HandleBoardAnswer;
            }
            if (weightedBoard != null)
            {
                weightedBoard.LinkResolved += HandleWeightedLinkResolved;
                weightedBoard.EndingResolveRequested += HandleEndingResolveRequested;
                weightedBoard.BoardSolved += HandleWeightedBoardSolved;
            }
            if (interrogation != null)
            {
                interrogation.InterrogationEnded += HandleInterrogationEnded;
                interrogation.MistrialRequested += HandleMistrialRequested;
                interrogation.RetryRequested += HandleRetryRequested;
                interrogation.StrikesChanged += HandleInterrogationStrikes;
                interrogation.StateChanged += HandleInterrogationStateChanged;
                interrogation.Forfeited += HandleInterrogationForfeited;
            }
            if (resolution != null)
                resolution.Dismissed += HandleResolutionDismissed;
            if (dialogue != null)
            {
                dialogue.ClueRegistered += HandleClueRegistered;
                dialogue.FlagSet += HandleDialogueFlagSet;
            }
            if (cutscenePlayer != null)
                cutscenePlayer.CutsceneEnded += HandleCutsceneEnded;
        }

        /// <summary>Detaches every subscription made by <see cref="Bind"/>.</summary>
        public void Unbind()
        {
            if (board != null)
            {
                board.FocusCosted -= HandleBoardFocusCosted;
                board.StrikeCosted -= HandleBoardStrikeCosted;
                board.InterrogationUnlocked -= HandleInterrogationUnlocked;
                board.HypothesisRoutesToEnding -= HandleRoutesToEnding;
                board.AnswerResolved -= HandleBoardAnswer;
            }
            if (weightedBoard != null)
            {
                weightedBoard.LinkResolved -= HandleWeightedLinkResolved;
                weightedBoard.EndingResolveRequested -= HandleEndingResolveRequested;
                weightedBoard.BoardSolved -= HandleWeightedBoardSolved;
            }
            if (interrogation != null)
            {
                interrogation.InterrogationEnded -= HandleInterrogationEnded;
                interrogation.MistrialRequested -= HandleMistrialRequested;
                interrogation.RetryRequested -= HandleRetryRequested;
                interrogation.StrikesChanged -= HandleInterrogationStrikes;
                interrogation.StateChanged -= HandleInterrogationStateChanged;
                interrogation.Forfeited -= HandleInterrogationForfeited;
            }
            if (resolution != null)
                resolution.Dismissed -= HandleResolutionDismissed;
            if (dialogue != null)
            {
                dialogue.ClueRegistered -= HandleClueRegistered;
                dialogue.FlagSet -= HandleDialogueFlagSet;
            }
            if (cutscenePlayer != null)
                cutscenePlayer.CutsceneEnded -= HandleCutsceneEnded;
        }

        /// <summary>Starts the composed investigation, accepted 2D encounter, or finale context.</summary>
        public void BeginCase(CaseData caseData)
        {
            if (caseData == null)
            {
                Debug.LogWarning("GameFlow: BeginCase called with a null CaseData.", this);
                return;
            }
            activeCase = caseData;
            _session = new CaseSession(caseData);
            var core = Core.CoreRuntime.Instance != null ? Core.CoreRuntime.Instance.Session : null;
            if (core != null)
            {
                var focus = Core.CoreRuntime.Instance.Focus;
                int focusCase = investigationScene && core.InvestigationCaseId >= 0
                    ? core.InvestigationCaseId : core.CurrentCase;
                if (focus.StrainedScanUsed && focusCase >= 1 && focusCase <= 4)
                    core.MarkStrainedScan(focusCase);
                if (investigationScene) core.InvestigationCaseId = caseData.caseId;
                else core.CurrentCase = caseData.caseId;
                focus.SetStrainedScanUsed(core.IsStrainedScanUsed(caseData.caseId));
                if (caseData.toolUnlocked != Tool.None) core.UnlockTool(caseData.toolUnlocked);
                latentLogCollected = core.IsLatentLogCollected(caseData.caseId);
                // A mistrial recorded before a checkpoint reload keeps the
                // Flawed penalty on the fresh case session.
                if (core.WasMistrialRetried(caseData.caseId))
                    _session.RecordMistrialRetry();
                else
                    _session.RestoreStrikes(
                        core.GetInterrogationStrikeCount(caseData.caseId));
            }
            _endingRequested = false;
            _endingCompleted = false;
            _mistrialHandled = false;
            _entryCutscenePending = false;
            _lastEnding = 0;
            _reportedStrikes = _session.StrikesLost;
            _collectedClues.Clear();
            Bind();
            bool encounter = core != null && !investigationScene &&
                             caseData.caseId >= 1 && caseData.caseId <= 4;
            if (weightedBoard != null)
                weightedBoard.CanUse = () => !encounter && !_endingRequested &&
                    !(journalPanel != null && journalPanel.IsOpen) &&
                    (investigationScene || caseData.caseId == 5 || Core.CoreRuntime.Instance == null);
            if (journalPanel != null)
                journalPanel.CanOpen = () =>
                    !_endingRequested &&
                    !(interrogation != null && interrogation.IsOpen) &&
                    !(weightedBoard != null && weightedBoard.IsOpen) &&
                    !(board != null && board.IsOpen) &&
                    !(resolution != null && resolution.IsVisible) &&
                    !(cutscenePlayer != null && cutscenePlayer.IsPlaying) &&
                    !(dialogue != null && dialogue.IsPlaying);
            if (encounter)
            {
                weightedBoard?.Close();
                weightedBoard?.SetCase(null);
                board?.Close();
                if (board != null) board.handleBoardHotkey = false;
                SyncBoardGradeInputs();
                if (core.CanStartInterrogation(caseData.caseId))
                    HandleInterrogationUnlocked(caseData.interrogation);
                else Debug.LogWarning("GameFlow: this 2D encounter requires an accepted investigation.", this);
                return;
            }
            bool weighted = weightedBoard != null && caseData.evidenceGraph != null
                            && weightedBoard.SetCase(caseData,
                                core != null
                                    ? core.GetOrCreateBoardState(caseData.caseId)
                                    : null);
            if (caseData.evidenceGraph != null && !weighted)
            {
                board?.Close();
                if (board != null) board.handleBoardHotkey = false;
                Debug.LogWarning("GameFlow: a valid weighted board panel is required; legacy fallback is disabled for this case.", this);
                return;
            }
            if (weighted)
            {
                var focus = Core.CoreRuntime.Instance != null
                    ? Core.CoreRuntime.Instance.Focus : null;
                weightedBoard.FocusSource = focus != null
                    ? () => (focus.Percent, focus.Maximum)
                    : (Func<(float, float)>)null;
                weightedBoard.FocusSink = focus != null
                    ? (energy, max) => focus.Sync(energy, max)
                    : (Action<float, float>)null;
                weightedBoard.SetClues(ResolveBoardClues(caseData, core));
            }
            if (board != null && !weighted)
            {
                board.SetCase(caseData);
                board.SetClues(ResolveBoardClues(caseData, core));
                // Rehydrate in-progress deductions after a Continue.
                if (core != null) board.RestoreValidatedNodes(core.BoardEdges);
            }
            else if (board != null)
            {
                board.Close();
            }
            if (board != null) board.handleBoardHotkey = !weighted;
            SyncBoardGradeInputs();

            // CaseLoop has no world content of its own — land the player on
            // the case board instead of an empty screen. If the board was
            // solved in a hub/investigation scene before CaseLoop opened, go
            // straight to the unlocked interrogation instead of dead-ending
            // on an already-solved board.
            // A once-per-save entry cutscene (the Case 5 "Echoes of the Past"
            // cinematic) delays the board until it finishes.
            if (entryCutscene != null && cutscenePlayer != null &&
                !caseData.hasInterrogation && core != null &&
                !core.HasFlag(EntryCutsceneFlagFor(caseData.caseId)))
            {
                core.SetFlag(EntryCutsceneFlagFor(caseData.caseId));
                _entryCutscenePending = true;
                cutscenePlayer.Play(entryCutscene);
                return;
            }
            OpenCaseBoard(weighted, caseData);
        }

        void OpenCaseBoard(bool weighted, CaseData caseData)
        {
            if (caseData == null) return;
            bool solvedWeighted = weighted && weightedBoard.Session != null &&
                                  weightedBoard.Session.State.BoardSolved;
            if (solvedWeighted && caseData.hasInterrogation &&
                caseData.interrogation != null)
                HandleInterrogationUnlocked(caseData.interrogation);
            else if (weighted) weightedBoard.Open();
            else board?.Open();
        }

        /// <summary>Session flag latching the once-per-save entry cutscene.</summary>
        static string EntryCutsceneFlagFor(int caseId) => $"Case{caseId}_EntryCutsceneSeen";

        /// <summary>
        /// M1 clue-rack rule: the case's authored preloadedClues when present
        /// (board-only cases such as Case 5). Otherwise, when a Core session
        /// exists, only the clues actually collected into it; without a session
        /// (standalone scenes/tests) every non-latent, non-backtracking case
        /// clue as before.
        /// </summary>
        public static List<ClueData> ResolveBoardClues(CaseData caseData)
            => ResolveBoardClues(caseData, null);

        static List<ClueData> ResolveBoardClues(CaseData caseData, Core.SessionState session)
        {
            var result = new List<ClueData>();
            if (caseData == null) return result;
            var preloaded = caseData.preloadedClues;
            if (preloaded != null && preloaded.Length > 0)
            {
                foreach (var clue in preloaded)
                    if (clue != null) result.Add(clue);
                return result;
            }
            if (caseData.clues == null) return result;
            foreach (var clue in caseData.clues)
            {
                if (clue == null || clue.type == ClueType.Latent || clue.isBacktrackingOnly)
                    continue;
                if (session != null && !session.HasClue(clue.id)) continue;
                result.Add(clue);
            }
            return result;
        }

        /// <summary>Resets the interrogation engine and reopens the panel (retry confirmed).</summary>
        public void RetryInterrogation()
        {
            if (interrogation == null) return;
            _mistrialHandled = false;
            interrogation.Reset();
            int caseId = _session != null ? _session.Case.caseId
                : activeCase != null ? activeCase.caseId : 0;
            PrepareInterrogationSnapshot(caseId);
            interrogation.Open();
        }

        // --- Board handlers (public for tests; subscribed in Bind) -------------

        public void HandleBoardFocusCosted(float cost)
        {
            Core.CoreRuntime.Instance?.Focus.Spend(cost);
            FocusSpent?.Invoke(cost, "Board");
        }

        public void HandleBoardStrikeCosted(int strikes)
        {
            if (_session == null || _endingRequested) return;
            _session.RecordStrikes(strikes);
            if (_session.StrikesLost >= InterrogationEngine.MaxStrikes)
                RequestEnding(1); // board fail -> Ending 1
        }

        public void HandleBoardAnswer(AnswerResult result)
        {
            if (result.Outcome == AnswerOutcome.Wrong)
                _session?.RecordWrongCausalAnswer();
            // Every answer can change the validated set - keep the session in
            // step so a mid-case checkpoint restores real board progress.
            if (board != null)
                Core.CoreRuntime.Instance?.Session.MergeBoardEdges(board.ValidatedNodeIds);
        }

        public void HandleInterrogationUnlocked(InterrogationData data)
        {
            if (interrogation == null || data == null) return;
            int caseId = _session != null ? _session.Case.caseId
                : activeCase != null ? activeCase.caseId : 0;
            // Guided first phase for the tutorial case; Emotion Echo auras
            // follow the actual unlocked tool, not the case index alone.
            var sessionTools = Core.CoreRuntime.Instance != null
                ? Core.CoreRuntime.Instance.Session.Tools : null;
            bool echo = sessionTools != null
                ? sessionTools.Contains(Tool.EmotionEcho) : caseId >= 3;
            interrogation.SetInterrogation(data, BuildEvidence(data),
                guided: caseId == 1, echo: echo);
            _mistrialHandled = false;
            board?.Close();
            weightedBoard?.Close();
            journalPanel?.Close();
            PrepareInterrogationSnapshot(caseId);
            interrogation.Open();
        }

        /// <summary>
        /// Restores an in-progress encounter snapshot when the session holds
        /// one for this case (Continue mid-interrogation), otherwise stamps a
        /// fresh entry snapshot so an interrupted attempt resumes coherently.
        /// Only 2D suspect encounters (cases 1-4) are persisted.
        /// </summary>
        void PrepareInterrogationSnapshot(int caseId)
        {
            var session = Core.CoreRuntime.Instance != null
                ? Core.CoreRuntime.Instance.Session : null;
            if (session == null || investigationScene || caseId < 1 || caseId > 4) return;
            var snapshot = session.ActiveInterrogation;
            if (snapshot != null && snapshot.caseId == caseId)
            {
                interrogation.RestoreState(snapshot.phaseIndex,
                    snapshot.composureSegmentsRemaining, snapshot.strikeCount);
                _reportedStrikes = snapshot.strikeCount;
                return;
            }
            session.SetActiveInterrogation(new Core.ActiveInterrogationSave
            {
                caseId = caseId,
                phaseIndex = interrogation.EnginePhaseIndex,
                composureSegmentsRemaining = interrogation.EngineSegmentsRemaining,
                strikeCount = interrogation.EngineStrikesLost,
                entryStrikeCount = session.GetInterrogationStrikeCount(caseId),
                preInterrogationCheckpointId = Core.GameStateManager.Instance != null &&
                                               Core.GameStateManager.Instance.CurrentSave != null
                    ? Core.GameStateManager.Instance.CurrentSave.checkpointSceneName ?? ""
                    : "",
            });
        }

        /// <summary>Keeps the session encounter snapshot in step with engine progress.</summary>
        public void HandleInterrogationStateChanged()
        {
            var snapshot = Core.CoreRuntime.Instance != null
                ? Core.CoreRuntime.Instance.Session.ActiveInterrogation : null;
            int caseId = _session != null ? _session.Case.caseId
                : activeCase != null ? activeCase.caseId : 0;
            if (snapshot == null || interrogation == null || snapshot.caseId != caseId) return;
            snapshot.phaseIndex = interrogation.EnginePhaseIndex;
            snapshot.composureSegmentsRemaining = interrogation.EngineSegmentsRemaining;
            snapshot.strikeCount = interrogation.EngineStrikesLost;
        }

        /// <summary>
        /// ESC forfeit: voids the aborted attempt (strikes rewind to the
        /// pre-encounter count, snapshot cleared) and returns the player to
        /// the case hub. Board acceptance and grade history are untouched.
        /// </summary>
        public void HandleInterrogationForfeited()
        {
            var core = Core.CoreRuntime.Instance;
            var session = core != null ? core.Session : null;
            var snapshot = session != null ? session.ActiveInterrogation : null;
            int caseId = snapshot != null ? snapshot.caseId
                : _session != null ? _session.Case.caseId
                : activeCase != null ? activeCase.caseId : 0;
            if (snapshot != null && session != null)
            {
                session.SetInterrogationStrikeCount(snapshot.caseId, snapshot.entryStrikeCount);
                _reportedStrikes = snapshot.entryStrikeCount;
                _session?.RestoreStrikes(snapshot.entryStrikeCount);
                session.SetActiveInterrogation(null);
            }
            if (core == null || caseId < 1 || caseId > 4) return;
            if (!Core.GameStateManager.Ensure().GoToRoute(
                    Core.RouteKeys.Hub(caseId), Core.GameFlowState.Hub))
                Debug.LogWarning("GameFlow: forfeited interrogation but no hub route is loadable.", this);
        }

        public void HandleRoutesToEnding()
        {
            if (_endingRequested) return;
            // Persistent session truth wins; the inspector field is only a
            // fallback for scenes running without a CoreRuntime.
            int logs = Core.CoreRuntime.Instance != null
                ? Core.CoreRuntime.Instance.Session.RunicLogCount
                : runicLogsCollected;
            RequestEnding(EndingSelector.Select(boardFailed: false, logs));
        }

        // --- Weighted board handlers (evidence-DAG cases) ----------------------

        /// <summary>Re-emits board Focus costs for HUD parity.</summary>
        public void HandleWeightedLinkResolved(IReadOnlyList<Morsoul.Engine.ProgressionEvent> events)
        {
            if (events == null) return;
            foreach (var e in events)
                if (e.Kind == Morsoul.Engine.ProgressionEventKind.LinkWrong)
                    FocusSpent?.Invoke(Morsoul.Engine.ProgressionConstants.ErrorFocusCost, "Board");
        }

        /// <summary>
        /// Case 5 unaffordable erroneous link: the board state still holds the
        /// pre-error Focus (unaffordable errors are never charged), so the
        /// Ending Resolver runs on it directly.
        /// </summary>
        public void HandleEndingResolveRequested()
        {
            if (_endingRequested || weightedBoard == null || weightedBoard.Session == null) return;
            var state = weightedBoard.Session.State;
            var result = Morsoul.Engine.EndingResolver.Resolve(new Morsoul.Engine.EndingResolverInput
            {
                PreErrorFocus = state.Focus,
                IncorrectLink = true,
                BoardSolved = state.BoardSolved,
                LogsCollected = CurrentLogCount(),
            });
            if (result.Ending.HasValue) RequestEnding(result.Ending.Value);
        }

        /// <summary>
        /// Weighted board solved: cases with an interrogation proceed to it;
        /// board-only cases (Case 5) resolve straight to an ending.
        /// </summary>
        public void HandleWeightedBoardSolved(Morsoul.Engine.DeductionCertificate cert)
        {
            var caseData = _session != null ? _session.Case : activeCase;
            if (caseData == null) return;
            if (caseData.caseId == 0)
            {
                weightedBoard?.Close();
                Core.CoreRuntime.Instance?.Session.RecordInvestigationSource(0, Core.InvestigationCompletionSource.Real);
                return;
            }
            if (caseData.hasInterrogation && caseData.interrogation != null)
            {
                var core = Core.CoreRuntime.Instance;
                if (core == null) HandleInterrogationUnlocked(caseData.interrogation);
                else
                {
                    weightedBoard?.Close();
                    core.Session.RecordInvestigationSource(caseData.caseId, Core.InvestigationCompletionSource.Real);
                    core.Bus.Publish(new Core.InvestigationStateChangedEvent { CaseId = caseData.caseId });
                    if (!Core.GameStateManager.Ensure().ReturnFromInvestigation(caseData.caseId))
                        Debug.LogWarning("GameFlow: the accepted investigation has no loadable 2D return location.", this);
                }
                return;
            }
            var result = Morsoul.Engine.EndingResolver.Resolve(new Morsoul.Engine.EndingResolverInput
            {
                PreErrorFocus = Core.CoreRuntime.Instance != null
                    ? Core.CoreRuntime.Instance.Focus.Percent : 100f,
                IncorrectLink = false,
                BoardSolved = true,
                LogsCollected = CurrentLogCount(),
            });
            if (result.Ending.HasValue) RequestEnding(result.Ending.Value);
        }

        int CurrentLogCount() =>
            Core.CoreRuntime.Instance != null
                ? Core.CoreRuntime.Instance.Session.RunicLogCount
                : runicLogsCollected;

        // --- Interrogation handlers --------------------------------------------

        public void HandleInterrogationEnded(InterrogationStatus status)
        {
            switch (status)
            {
                case InterrogationStatus.Confessed:
                    HandleConfessed();
                    break;
                case InterrogationStatus.Mistrial:
                    HandleMistrialRequested();
                    break;
            }
        }

        /// <summary>
        /// Mistrial path shared by MistrialRequested and InterrogationEnded(Mistrial);
        /// the controller fires both, so the handler is deduplicated via _mistrialHandled.
        /// Only records the penalty — the actual checkpoint reload waits for the
        /// player's Retry confirmation (<see cref="HandleRetryRequested"/>).
        /// </summary>
        public void HandleMistrialRequested()
        {
            if (_session == null || _mistrialHandled) return;
            _mistrialHandled = true;
            _session.RecordMistrialRetry();
            _reportedStrikes = 0;
            // Persist the penalty and the reset strike count through the retry;
            // the encounter snapshot ends so the reload starts a fresh attempt.
            var coreSession = Core.CoreRuntime.Instance?.Session;
            coreSession?.SetFlag(
                Core.SessionState.MistrialFlagFor(_session.Case.caseId));
            coreSession?.SetInterrogationStrikeCount(_session.Case.caseId, 0);
            coreSession?.SetActiveInterrogation(null);
        }

        /// <summary>
        /// "Retry from Checkpoint" confirmed. With a live CoreRuntime the GSM
        /// consumes MistrialRetryEvent and reloads the case checkpoint; without
        /// one we fall back to an in-scene engine reset.
        /// </summary>
        public void HandleRetryRequested()
        {
            if (!_mistrialHandled) return;
            MistrialRetryRequested?.Invoke();
            if (Core.CoreRuntime.Instance != null)
                Core.CoreRuntime.Instance.Bus.Publish(new Core.MistrialRetryEvent());
            else
                RetryInterrogation();
        }

        /// <summary>Feeds interrogation-reported strike totals into the grade model.</summary>
        public void HandleInterrogationStrikes(int total)
        {
            if (_session == null) return;
            int delta = total - _reportedStrikes;
            _reportedStrikes = total;
            if (delta > 0) _session.RecordStrikes(delta);
            Core.CoreRuntime.Instance?.Session.SetInterrogationStrikeCount(
                _session.Case.caseId, total);
        }

        /// <summary>Dialogue-set flags flow into the hub flag set + Core session.</summary>
        public void HandleDialogueFlagSet(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return;
            if (flags != null) flags.Add(flag);
            else Core.CoreRuntime.Instance?.Session.SetFlag(flag);
        }

        // --- Resolution / routing ----------------------------------------------

        /// <summary>
        /// Resolution screen dismissed: publishes CaseCompletedEvent so the
        /// GSM can route to the next hub (or Title on a game ending).
        /// </summary>
        public void HandleResolutionDismissed()
        {
            if (_endingRequested)
            {
                CompleteEnding();
                return;
            }
            var caseData = _session != null ? _session.Case : activeCase;
            Core.CoreRuntime.Instance?.Bus.Publish(new Core.CaseCompletedEvent
            {
                CaseId = caseData != null ? caseData.caseId : 0,
                Ending = _lastEnding
            });
        }

        // --- Dialogue handler ----------------------------------------------------

        public void HandleClueRegistered(ClueData clue)
        {
            if (clue == null) return;
            if (!_collectedClues.Contains(clue)) _collectedClues.Add(clue);
            hubJournal?.Register(clue);
            var session = Core.CoreRuntime.Instance != null
                ? Core.CoreRuntime.Instance.Session : null;
            session?.RegisterClue(clue);
            if (clue.type == ClueType.Latent)
            {
                // Latches the per-case unease flag and (via the L<n>_<case>
                // id convention) the matching runic-log slot in the session.
                session?.RegisterLatentClue(clue);
                if (activeCase != null && clue.caseId == activeCase.caseId)
                    latentLogCollected = true;
                // Heeding the banner resolves it: clear the owning case's
                // unease flag so the journal's dissonance entry disappears
                // once the log is recovered (including backtrack revisits).
                if (session != null)
                {
                    var owner = Core.CoreRuntime.Instance.ResolveCase(clue.caseId);
                    if (owner != null && !string.IsNullOrEmpty(owner.uneaseFlagKey))
                        session.SetFlag(owner.uneaseFlagKey, false);
                }
            }
            ClueRegistered?.Invoke(clue);
        }

        /// <summary>
        /// Ending cutscene finished: completes the case so the GSM routes to
        /// Title; entry cutscene finished: opens the waiting case board.
        /// Ignores cutscene ends unrelated to either.
        /// </summary>
        public void HandleCutsceneEnded()
        {
            if (_endingRequested)
            {
                CompleteEnding();
                return;
            }
            if (_entryCutscenePending)
            {
                _entryCutscenePending = false;
                bool weighted = weightedBoard != null && weightedBoard.Session != null;
                OpenCaseBoard(weighted, _session != null ? _session.Case : activeCase);
            }
        }

        // --- Internals ------------------------------------------------------------

        void SyncBoardGradeInputs()
        {
            if (_session == null || !_session.IsGraded) return;
            var boardState = weightedBoard != null && weightedBoard.Session != null
                ? weightedBoard.Session.State
                : null;
            var session = Core.CoreRuntime.Instance != null
                ? Core.CoreRuntime.Instance.Session : null;
            if (boardState == null && session != null)
                session.BoardStates.TryGetValue(_session.Case.caseId, out boardState);
            if (boardState == null || boardState.CaseId != _session.Case.caseId)
                return;
            _session.SetBoardGradeInputs(
                boardState.CaseErrorCount,
                boardState.SubmittedWrongConclusionIds != null &&
                    boardState.SubmittedWrongConclusionIds.Count > 0,
                boardState.FiredHintTiers != null &&
                    boardState.FiredHintTiers.Count > 0);
        }

        void HandleConfessed()
        {
            var caseData = _session != null ? _session.Case : activeCase;
            if (caseData == null) return;
            var session = Core.CoreRuntime.Instance != null ? Core.CoreRuntime.Instance.Session : null;
            SyncBoardGradeInputs();
            session?.SetActiveInterrogation(null);
            // Strained scans during the investigation pull the grade to Standard.
            if (Core.CoreRuntime.Instance != null
                && Core.CoreRuntime.Instance.Focus.StrainedScanUsed)
            {
                _session?.RecordStrainedScan();
                session?.MarkStrainedScan(caseData.caseId);
            }
            Grade grade = _session != null ? _session.ComputeGrade() : Grade.Ungraded;
            bool unease;
            if (session != null)
            {
                session.SetGrade(caseData.caseId, grade);
                if (caseData.caseId == 4) session.SetFlag("Case4_BrokkFreed");
                session.SetFlag(Core.SessionState.LatentLogFlagFor(caseData.caseId),
                    latentLogCollected);
                // Evaluate after the resolution-side flags land so unlocks
                // granted by this confession (Case4_BrokkFreed) count as
                // reachable — GDD §2.4 fires the banner only for evidence the
                // player's current tools could actually reach.
                unease = caseData.latentLog != null && !latentLogCollected &&
                         LatentLogReachable(caseData, session);
                if (!string.IsNullOrEmpty(caseData.uneaseFlagKey))
                    session.SetFlag(caseData.uneaseFlagKey, unease);
                session.LatentLogCollected = session.LatentLogCollected || latentLogCollected;
                if (board != null) session.MergeBoardEdges(board.ValidatedNodeIds);
            }
            else
            {
                // No session to evaluate eligibility against — standalone
                // scenes/tests keep the legacy "missing log" semantics.
                unease = caseData.latentLog != null && !latentLogCollected;
            }
            resolution?.Show(caseData.title,
                grade,
                unease,
                caseData.latentLog != null ? caseData.latentLog.location : null,
                null);
            interrogation?.Close();
        }

        /// <summary>
        /// GDD §2.4 reachability for the Unease banner: the case's Latent clue
        /// must pass the shared acquisition rules (required flag, required
        /// tool, availability window, combined-tool methods) against the live
        /// session. Returns false when the case has no Latent clue to check.
        /// </summary>
        static bool LatentLogReachable(CaseData caseData, Core.SessionState session)
        {
            var clue = caseData.clues != null
                ? System.Array.Find(caseData.clues,
                    c => c != null && c.type == ClueType.Latent)
                : null;
            if (clue == null) return false;
            return Core.ClueAcquisition.CanAcquire(clue, session, caseData.latentLog, out _);
        }

        /// <summary>
        /// Weighted interrogations use collected clues, not generated premises.
        /// Journal/dialogue evidence is rehydrated from Session so a fresh
        /// CaseLoop entry retains clues gathered in earlier environments.
        /// Verified premises remain available only to the legacy compatibility
        /// path for cases without an authored evidence graph.
        /// </summary>
        public List<EvidenceData> BuildEvidence(InterrogationData interrogationData = null)
        {
            var evidence = new List<EvidenceData>();
            var seen = new HashSet<EvidenceData>();
            void Add(EvidenceData item)
            {
                if (item != null && seen.Add(item)) evidence.Add(item);
            }

            var caseData = _session != null ? _session.Case : activeCase;
            var session = Core.CoreRuntime.Instance != null
                ? Core.CoreRuntime.Instance.Session : null;
            if (session != null && caseData != null && caseData.clues != null)
                foreach (var clue in caseData.clues)
                    if (clue != null && session.HasClue(clue.id)) Add(clue);
            if (hubJournal != null)
                foreach (var clue in hubJournal.Collected) Add(clue);
            foreach (var clue in _collectedClues) Add(clue);
            if (caseData != null && caseData.evidenceGraph == null && board != null && board.Premises != null)
                foreach (var premise in board.Premises) Add(premise);
            // Weighted boards don't mint PremiseData at runtime — an
            // interrogation only opens once its board is solved, so the
            // deduction premises its valid moves ask for are earned content
            // by construction. ClueData stays collection-gated above.
            if (interrogationData != null && caseData != null &&
                caseData.evidenceGraph != null && interrogationData.phases != null)
                foreach (var phase in interrogationData.phases)
                    if (phase != null && phase.validMoves != null)
                        foreach (var move in phase.validMoves)
                            if (move != null && move.evidence is PremiseData)
                                Add(move.evidence);
            return evidence;
        }

        void RequestEnding(int ending)
        {
            _endingRequested = true;
            _lastEnding = ending;
            weightedBoard?.Close();
            board?.Close();
            interrogation?.Close();
            journalPanel?.Close();
            Core.CoreRuntime.Instance?.Session.SetActiveInterrogation(null);
            EndingRouteRequested?.Invoke(ending);
            var cutscene = endingCutscenes != null
                           && ending >= 0 && ending < endingCutscenes.Length
                ? endingCutscenes[ending] : null;
            if (cutscenePlayer != null && cutscene != null)
                cutscenePlayer.Play(cutscene); // HandleCutsceneEnded completes the case
            else
                CompleteEnding();
        }

        /// <summary>Publishes case completion exactly once (ending cutscene or dismissal).</summary>
        void CompleteEnding()
        {
            if (_endingCompleted) return;
            _endingCompleted = true;
            var caseData = _session != null ? _session.Case : activeCase;
            Core.CoreRuntime.Instance?.Bus.Publish(new Core.CaseCompletedEvent
            {
                CaseId = caseData != null ? caseData.caseId : 0,
                Ending = _lastEnding
            });
        }
    }
}
