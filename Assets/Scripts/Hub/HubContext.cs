using System;
using System.Collections.Generic;
using Morsoul.Board;
using Morsoul.Core;
using Morsoul.Data;
using Morsoul.Dialogue;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Scene-level wiring hub for 2D hub scenes. Holds the shared services that
    /// <see cref="Interactable2D"/> implementations need when interacted with:
    /// the dialogue overlay controller, the hub clue journal and the scene flag
    /// set. No static singletons — every interactable receives this context
    /// through <see cref="Interactable2D.Interact"/>.
    ///
    /// Also the hub's single dialogue-effects binder and overlay-blocker
    /// registry: dialogue clue/flag side effects are forwarded into the
    /// journal/flag stores exactly once here, and overlays (journal, rest,
    /// cutscene) register blockers so <see cref="UiBlocked"/> stays the one
    /// movement/interaction gate.
    /// </summary>
    public class HubContext : MonoBehaviour
    {
        public DialogueController dialogue;
        public HubJournal journal;
        public HubFlags flags;
        public HubHudController hud;
        /// <summary>Optional carrier travel menu; opened by CarrierInteractable.</summary>
        public TravelMenuController travelMenu;
        public WeightedBoardController boardPanel;

        readonly List<Func<bool>> _uiBlockers = new List<Func<bool>>();
        DialogueController _boundDialogue;
        Func<bool> _pauseModalPredicate;

        void OnEnable()
        {
            BindDialogueEffects();
            _pauseModalPredicate ??= () =>
                DialoguePlaying ||
                (travelMenu != null && travelMenu.IsOpen) ||
                (ResolveBoardPanel() != null && boardPanel.IsOpen);
            PauseMenuController.RegisterExternalModal(_pauseModalPredicate);
        }

        void OnDisable()
        {
            if (_pauseModalPredicate != null)
                PauseMenuController.UnregisterExternalModal(_pauseModalPredicate);
            if (_boundDialogue != null)
            {
                _boundDialogue.ClueRegistered -= OnDialogueClueRegistered;
                _boundDialogue.FlagSet -= OnDialogueFlagSet;
                _boundDialogue = null;
            }
        }

        /// <summary>True when a dialogue is currently playing (blocks movement/interaction).</summary>
        public bool DialoguePlaying => dialogue != null && dialogue.IsPlaying;

        /// <summary>
        /// True while any blocking overlay (dialogue, travel menu, board,
        /// registered blocker such as the journal or a rest spot) is open, or
        /// while a session cutscene is active.
        /// </summary>
        public bool UiBlocked
        {
            get
            {
                if (PauseMenuController.IsPaused ||
                    DialoguePlaying ||
                    (travelMenu != null && travelMenu.IsOpen) ||
                    (ResolveBoardPanel() != null && boardPanel.IsOpen) ||
                    CutsceneActive)
                    return true;
                for (int i = _uiBlockers.Count - 1; i >= 0; i--)
                {
                    var blocker = _uiBlockers[i];
                    if (blocker == null)
                    {
                        _uiBlockers.RemoveAt(i);
                        continue;
                    }
                    if (blocker()) return true;
                }
                return false;
            }
        }

        /// <summary>True while the session reports an in-progress cutscene.</summary>
        public bool CutsceneActive =>
            CoreRuntime.Instance != null &&
            !string.IsNullOrEmpty(CoreRuntime.Instance.Session.ActiveCutsceneId);

        /// <summary>Registers an overlay blocker consulted by <see cref="UiBlocked"/>.</summary>
        public void AddUiBlocker(Func<bool> blocker)
        {
            if (blocker != null && !_uiBlockers.Contains(blocker))
                _uiBlockers.Add(blocker);
        }

        /// <summary>Removes a blocker previously added with <see cref="AddUiBlocker"/>.</summary>
        public void RemoveUiBlocker(Func<bool> blocker)
        {
            if (blocker != null) _uiBlockers.Remove(blocker);
        }

        /// <summary>
        /// Plays a DialogueData through the shared controller with the scene's
        /// flag set, the session's saved player name and the single
        /// journal/flag effects binding. Returns false when nothing can play.
        /// </summary>
        public bool PlayDialogue(DialogueData data)
        {
            if (dialogue == null || data == null)
            {
                Debug.LogWarning($"HubContext '{name}': PlayDialogue with no controller/data.", this);
                return false;
            }
            BindDialogueEffects();
            if (flags != null) dialogue.SetFlags(flags.Set);
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (session != null && !string.IsNullOrEmpty(session.PlayerName))
                dialogue.playerName = session.PlayerName;
            dialogue.SetDialogue(data);
            dialogue.Play();
            return dialogue.IsPlaying;
        }

        /// <summary>Attaches the scene's one dialogue-effects binding; idempotent.</summary>
        void BindDialogueEffects()
        {
            if (_boundDialogue == dialogue || dialogue == null) return;
            if (_boundDialogue != null)
            {
                _boundDialogue.ClueRegistered -= OnDialogueClueRegistered;
                _boundDialogue.FlagSet -= OnDialogueFlagSet;
            }
            _boundDialogue = dialogue;
            _boundDialogue.ClueRegistered += OnDialogueClueRegistered;
            _boundDialogue.FlagSet += OnDialogueFlagSet;
        }

        void OnDialogueClueRegistered(ClueData clue) => journal?.Register(clue);
        void OnDialogueFlagSet(string flag) => flags?.Add(flag);

        WeightedBoardController ResolveBoardPanel()
        {
            if (boardPanel == null)
                boardPanel = FindAnyObjectByType<WeightedBoardController>(
                    FindObjectsInactive.Include);
            return boardPanel;
        }
    }
}
