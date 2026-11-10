namespace Morsoul.Core
{
    public struct InvestigationEntryResult
    {
        public bool Success;
        public string Message;
    }

    public struct InvestigationStateChangedEvent
    {
        public int CaseId;
    }

    /// <summary>Published when the title screen requests a new game.</summary>
    public struct NewGameEvent
    {
        public string PlayerName;
    }

    /// <summary>Published when the title screen requests continue-from-save.</summary>
    public struct ContinueGameEvent { }

    /// <summary>Published right before a scene transition; a checkpoint is written.</summary>
    public struct CheckpointRequestedEvent
    {
        /// <summary>Why the checkpoint was written (Seam, Carrier, Mistrial, ...).</summary>
        public string Reason;
        /// <summary>Scene the game is transitioning INTO (null = current scene).</summary>
        public string NextSceneName;
    }

    /// <summary>Published when a scene load should happen through the GSM.</summary>
    public struct SceneLoadRequestedEvent
    {
        public string SceneName;
    }

    /// <summary>Published when a case finishes and the run advances.</summary>
    public struct CaseCompletedEvent
    {
        public int CaseId;
        public int Ending; // 1 = board fail, 2/3 = EndingSelector
    }

    /// <summary>
    /// Published on interrogation mistrial — "Retry from Checkpoint" reloads
    /// the Zone B entry checkpoint (composure and strikes reset).
    /// </summary>
    public struct MistrialRetryEvent { }
}
