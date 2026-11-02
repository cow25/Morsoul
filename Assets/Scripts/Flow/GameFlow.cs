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
        [Tooltip("Runic logs collected so far (save-side stub; EndingSelector threshold is 4).")]
        public int runicLogsCollected;
        [Tooltip("True once the active case's latent log clue has been collected.")]
        public bool latentLogCollected;

        [Header("Panel controllers")]
        public BoardController board;
        public InterrogationController interrogation;
        public ResolutionController resolution;
        [Tooltip("Reserved for intro/outro playback; not driven by the case loop yet.")]
        public CutscenePlayerController cutscenePlayer;
        [Tooltip("Optional. Clues registered by dialogue feed the journal + evidence.")]
        public DialogueController dialogue;
        [Tooltip("Optional. Hub-scene clue store; absent outside hub scenes.")]
        public HubJournal hubJournal;

        /// <summary>Raised when the run should route to an ending (1 = board fail, 2/3 = EndingSelector).</summary>
        public event Action<int> EndingRouteRequested;
        /// <summary>Raised once per mistrial; a checkpoint reload consumes it later.</summary>
        public event Action MistrialRetryRequested;
        /// <summary>Re-emitted Focus costs for the (future) FocusModel. Second arg is the source.</summary>
        public event Action<float, string> FocusSpent;
        /// <summary>Re-emitted when dialogue registers a clue.</summary>
        public event Action<ClueData> ClueRegistered;

        CaseSession _session;
        readonly List<ClueData> _collectedClues = new List<ClueData>();
        bool _endingRequested;
        bool _mistrialHandled;
        int _lastEnding;

        public CaseSession Session => _session;
        /// <summary>Grade of the current session (Ungraded before BeginCase) — used by save.</summary>
        public Grade CurrentGrade => _session != null ? _session.ComputeGrade() : Grade.Ungraded;

        void OnEnable()
        {
            Bind();
            if (activeCase != null) BeginCase(activeCase);
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
            }
            if (resolution != null)
                resolution.Dismissed += HandleResolutionDismissed;
            if (dialogue != null)
                dialogue.ClueRegistered += HandleClueRegistered;
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
            }
            if (resolution != null)
                resolution.Dismissed -= HandleResolutionDismissed;
            if (dialogue != null)
                dialogue.ClueRegistered -= HandleClueRegistered;
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
                latentLogCollected = core.LatentLogCollected;
            }
            _endingRequested = false;
            _mistrialHandled = false;
            _lastEnding = 0;
            _collectedClues.Clear();
            Bind();
            if (board != null)
            {
                board.SetCase(caseData);
                board.SetClues(ResolveBoardClues(caseData));
            }
        }

        /// <summary>
        /// M1 clue-rack rule: the case's authored preloadedClues when present
        /// (board-only cases such as Case 5), otherwise every non-latent,
        /// non-backtracking case clue. Journal-driven collection replaces this
        /// once hub scenes feed clues into the session.
        /// </summary>
        public static List<ClueData> ResolveBoardClues(CaseData caseData)
        {
            var result = new List<ClueData>();
            if (caseData == null) return result;
            var preloaded = caseData.preloadedClues;
            IEnumerable<ClueData> source = preloaded != null && preloaded.Length > 0
                ? preloaded
                : caseData.clues;
            if (source == null) return result;
            foreach (var clue in source)
                if (clue != null && clue.type != ClueType.Latent && !clue.isBacktrackingOnly)
                    result.Add(clue);
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
            RequestEnding(EndingSelector.Select(boardFailed: false, runicLogsCollected));
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
        /// </summary>
        public void HandleMistrialRequested()
        {
            if (_session == null || _mistrialHandled) return;
            _mistrialHandled = true;
            _session.RecordMistrialRetry();
            MistrialRetryRequested?.Invoke();
            Core.CoreRuntime.Instance?.Bus.Publish(new Core.MistrialRetryEvent());
        }

        // --- Resolution / routing ----------------------------------------------

        /// <summary>
        /// Resolution screen dismissed: publishes CaseCompletedEvent so the
        /// GSM can route to the next hub (or Title on a game ending).
        /// </summary>
        public void HandleResolutionDismissed()
        {
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
            Core.CoreRuntime.Instance?.Session.RegisterClue(clue);
            ClueRegistered?.Invoke(clue);
        }

        // --- Internals ------------------------------------------------------------

        void HandleConfessed()
        {
            var caseData = _session != null ? _session.Case : activeCase;
            if (caseData == null) return;
            // Unease fires when the case hides a latent log the player never found.
            bool unease = caseData.latentLog != null && !latentLogCollected;
            Grade grade = _session != null ? _session.ComputeGrade() : Grade.Ungraded;
            var session = Core.CoreRuntime.Instance != null ? Core.CoreRuntime.Instance.Session : null;
            if (session != null)
            {
                session.SetGrade(caseData.caseId, grade);
                session.LatentLogCollected = latentLogCollected;
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
            EndingRouteRequested?.Invoke(ending);
        }
    }
}
