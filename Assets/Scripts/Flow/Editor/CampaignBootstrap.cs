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
            Morsoul.Hub.Editor.HubBoardSetup.Install();
            Morsoul.Hub.Editor.DemoStandInFactory.BuildAll();
        }

        /// <summary>
        /// Batch entry point: full campaign wiring + provisional graphs audit,
        /// then exit so -executeMethod invocations do not linger.
        /// </summary>
        public static void RunAllBatch()
        {
            RunAll();
            Morsoul.Data.Editor.EvidenceGraphAuditor.AuditAll();
            EditorApplication.Exit(0);
        }
    }
}
