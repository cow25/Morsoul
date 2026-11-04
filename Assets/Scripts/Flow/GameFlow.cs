using System;
using System.Collections.Generic;
using Morsoul.Board;
using Morsoul.Cutscene;
using Morsoul.Data;
using Morsoul.Dialogue;
using Morsoul.Gameplay;
using Morsoul.Hub;
using Morsoul.Interrogation;
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

        [Header("Panel controllers")]
        public BoardController board;
        public InterrogationController interrogation;
        public ResolutionController resolution;
        [Tooltip("Plays the ending cutscene selected by the case loop.")]
        public CutscenePlayerController cutscenePlayer;
        [Tooltip("Optional. Clues registered by dialogue feed the journal + evidence.")]
        public DialogueController dialogue;
        [Tooltip("Optional. Hub-scene clue store; absent outside hub scenes.")]
        public HubJournal hubJournal;
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
        int _lastEnding;
        int _reportedStrikes;

        public CaseSession Session => _session;
        /// <summary>Grade of the current session (Ungraded before BeginCase) — used by save.</summary>
        public Grade CurrentGrade => _session != null ? _session.ComputeGrade() : Grade.Ungraded;

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
            var core = Core.CoreRuntime.Instance;
            if (core == null)
            {
                Debug.LogWarning("GameFlow: no activeCase assigned and no CoreRuntime — nothing to begin.", this);
                return false;
            }
            var data = caseDatabase != null
                ? caseDatabase.Find(core.Session.CurrentCase)
                : core.ResolveCase(core.Session.CurrentCase);
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
            if (interrogation != null)
            {
                interrogation.InterrogationEnded += HandleInterrogationEnded;
                interrogation.MistrialRequested += HandleMistrialRequested;
                interrogation.RetryRequested += HandleRetryRequested;
                interrogation.StrikesChanged += HandleInterrogationStrikes;
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
            if (interrogation != null)
            {
                interrogation.InterrogationEnded -= HandleInterrogationEnded;
                interrogation.MistrialRequested -= HandleMistrialRequested;
                interrogation.RetryRequested -= HandleRetryRequested;
                interrogation.StrikesChanged -= HandleInterrogationStrikes;
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

        /// <summary>Starts a case: fresh session, case + clue rack pushed into the board.</summary>
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
                core.CurrentCase = caseData.caseId;
                if (caseData.toolUnlocked != Tool.None) core.UnlockTool(caseData.toolUnlocked);
                latentLogCollected = core.IsLatentLogCollected(caseData.caseId);
                // A mistrial recorded before a checkpoint reload keeps the
                // Flawed penalty on the fresh case session.
                if (core.WasMistrialRetried(caseData.caseId))
                    _session.RecordMistrialRetry();
            }
            _endingRequested = false;
            _endingCompleted = false;
            _mistrialHandled = false;
            _lastEnding = 0;
            _reportedStrikes = 0;
            _collectedClues.Clear();
            Bind();
            if (board != null)
            {
                board.SetCase(caseData);
                board.SetClues(ResolveBoardClues(caseData, core));
                // Rehydrate in-progress deductions after a Continue.
                if (core != null) board.RestoreValidatedNodes(core.BoardEdges);
            }
        }

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
            // Guided first phase for the tutorial case; Emotion Echo auras from case 3 on.
            interrogation.SetInterrogation(data, BuildEvidence(),
                guided: caseId == 1, echo: caseId >= 3);
            _mistrialHandled = false;
            board?.Close();
            interrogation.Open();
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
            // Persist the penalty so a checkpoint reload still grades Flawed.
            Core.CoreRuntime.Instance?.Session.SetFlag(
                Core.SessionState.MistrialFlagFor(_session.Case.caseId));
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
            }
            ClueRegistered?.Invoke(clue);
        }

        /// <summary>
        /// Ending cutscene finished: completes the case so the GSM routes to
        /// Title. Ignores cutscene ends unrelated to a requested ending.
        /// </summary>
        public void HandleCutsceneEnded()
        {
            if (_endingRequested) CompleteEnding();
        }

        // --- Internals ------------------------------------------------------------

        void HandleConfessed()
        {
            var caseData = _session != null ? _session.Case : activeCase;
            if (caseData == null) return;
            var session = Core.CoreRuntime.Instance != null ? Core.CoreRuntime.Instance.Session : null;
            // Strained scans during the investigation pull the grade to Standard.
            if (Core.CoreRuntime.Instance != null
                && Core.CoreRuntime.Instance.Focus.StrainedScanUsed)
                _session?.RecordStrainedScan();
            // Unease fires when the case hides a latent log the player never found.
            bool unease = caseData.latentLog != null && !latentLogCollected;
            Grade grade = _session != null ? _session.ComputeGrade() : Grade.Ungraded;
            if (session != null)
            {
                session.SetGrade(caseData.caseId, grade);
                session.SetFlag(Core.SessionState.LatentLogFlagFor(caseData.caseId),
                    latentLogCollected);
                session.LatentLogCollected = session.LatentLogCollected || latentLogCollected;
                if (board != null) session.MergeBoardEdges(board.ValidatedNodeIds);
            }
            resolution?.Show(caseData.title,
                grade,
                unease,
                caseData.latentLog != null ? caseData.latentLog.location : null,
                null);
            interrogation?.Close();
        }

        /// <summary>Interrogation evidence = journal-/dialogue-collected clues + verified board premises.</summary>
        List<EvidenceData> BuildEvidence()
        {
            var evidence = new List<EvidenceData>();
            var seen = new HashSet<EvidenceData>();
            void Add(EvidenceData item)
            {
                if (item != null && seen.Add(item)) evidence.Add(item);
            }
            if (hubJournal != null)
                foreach (var clue in hubJournal.Collected) Add(clue);
            foreach (var clue in _collectedClues) Add(clue);
            if (board != null && board.Premises != null)
                foreach (var premise in board.Premises) Add(premise);
            return evidence;
        }

        void RequestEnding(int ending)
        {
            _endingRequested = true;
            _lastEnding = ending;
            board?.Close();
            interrogation?.Close();
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
