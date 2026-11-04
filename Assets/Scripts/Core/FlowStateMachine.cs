using System;
using System.Collections.Generic;

namespace Morsoul.Core
{
    /// <summary>Top-level game states (GDD case loop).</summary>
    public enum GameFlowState
    {
        Boot,
        Title,
        Opening,
        Hub,
        Investigation,
        Board,
        Interrogation,
        Resolution,
        Ending
    }

    /// <summary>
    /// Pure FSM for the case loop:
    /// Title -> Opening -> Hub -> Investigation -> Board -> Interrogation
    ///   -> Resolution -> next case / Ending.
    /// Board, Interrogation, and Resolution can be panels inside a scene
    /// rather than scene loads — the state exists either way so save/flow
    /// logic can always answer "where are we".
    /// </summary>
    public class FlowStateMachine
    {
        static readonly Dictionary<GameFlowState, GameFlowState[]> Legal =
            new Dictionary<GameFlowState, GameFlowState[]>
            {
                { GameFlowState.Boot, new[] { GameFlowState.Title } },
                { GameFlowState.Title, new[] { GameFlowState.Opening, GameFlowState.Hub } },
                { GameFlowState.Opening, new[] { GameFlowState.Hub } },
                { GameFlowState.Hub, new[] { GameFlowState.Investigation, GameFlowState.Board, GameFlowState.Title } },
                { GameFlowState.Investigation, new[] { GameFlowState.Hub, GameFlowState.Board } },
                { GameFlowState.Board, new[] { GameFlowState.Interrogation, GameFlowState.Ending, GameFlowState.Hub } },
                { GameFlowState.Interrogation, new[] { GameFlowState.Investigation, GameFlowState.Resolution, GameFlowState.Ending } },
                { GameFlowState.Resolution, new[] { GameFlowState.Hub, GameFlowState.Ending, GameFlowState.Title } },
                { GameFlowState.Ending, new[] { GameFlowState.Title } },
            };

        public GameFlowState Current { get; private set; } = GameFlowState.Boot;

        /// <summary>Fired after a successful transition.</summary>
        public event Action<GameFlowState, GameFlowState> StateChanged;

        /// <summary>True when the transition is in the legal table.</summary>
        public bool CanTransitionTo(GameFlowState next)
        {
            GameFlowState[] allowed;
            return Legal.TryGetValue(Current, out allowed)
                && Array.IndexOf(allowed, next) >= 0;
        }

        /// <summary>
        /// Transitions if legal. Returns false (no state change) otherwise —
        /// callers that need a reason can check CanTransitionTo first.
        /// </summary>
        public bool TryTransition(GameFlowState next)
        {
            if (!CanTransitionTo(next)) return false;
            var prev = Current;
            Current = next;
            StateChanged?.Invoke(prev, next);
            return true;
        }
    }
}
