using UnityEditor;

namespace Morsoul.Flow.Editor
{
    /// <summary>
    /// One-shot campaign wiring refresh: rebuilds the case database index and
    /// installs the case-loop entry interactable into the saved hub scenes.
    /// Run after adding case content or pulling hub scene changes.
    /// </summary>
    public static class CampaignBootstrap
    {
        [MenuItem("Morsoul/Setup/Rebuild Campaign Wiring")]
        public static void RunAll()
        {
            Morsoul.Data.Editor.CaseDatabaseGenerator.Rebuild();
            Morsoul.Hub.Editor.HubCaseLoopSetup.Install();
        }
    }
}
