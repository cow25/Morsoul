using System.Collections.Generic;
using Morsoul.Core;
using Morsoul.Data;

namespace Morsoul.Dev
{
    /// <summary>
    /// Named session/game states for one-click testing. Each scenario mutates
    /// SessionState (flags, clues, current case) and optionally jumps to a
    /// route. Flag names mirror the generator constants (integration
    /// contracts — keep in sync with HubDialogueContentGenerator /
    /// DialogueContentGenerator / HubContentSetup).
    /// </summary>
    public static class DevScenarios
    {
        // Gate-grant flags (must match the serialized requiredFlag values).
        public const string WarrantFlag = "C1_WarrantGranted";
        public const string BelfryKeyFlag = "C2_BelfryKeyGranted";
        public const string PerimeterSealFlag = "C3_PerimeterSealGranted";
        public const string OverrideClearanceFlag = "C4_OverrideClearance";

        // Interlude once-flags installed by HubContentSetup.
        public static readonly string[] InterludeFlags =
        {
            "Interlude.C1.Played", "Interlude.C2.Played", "Interlude.C3.Played"
        };

        public class Scenario
        {
            public string name;
            public string description;
            public int? currentCase;
            public string routeKey;           // optional travel after apply
            public bool collectCaseClues;
            public string[] setFlags;
            public string[] clearFlags;
            public bool resetSession;
        }

        public static readonly List<Scenario> All = new List<Scenario>
        {
            new Scenario
            {
                name = "Fresh session",
                description = "Reset SessionState entirely (flags, clues, case, logs).",
                resetSession = true,
            },
            new Scenario
            {
                name = "C1: warrant granted",
                description = "Case 1, warrant flag set → vault gate opens.",
                currentCase = 1, routeKey = "hub.1",
                setFlags = new[] { WarrantFlag },
            },
            new Scenario
            {
                name = "C2: board ready",
                description = "Case 2, hub clues collected and Belfry Key granted.",
                currentCase = 2, routeKey = "hub.2", collectCaseClues = true,
                setFlags = new[] { BelfryKeyFlag },
            },
            new Scenario
            {
                name = "C3: board ready",
                description = "Case 3, hub clues collected and Perimeter Seal granted.",
                currentCase = 3, routeKey = "hub.3", collectCaseClues = true,
                setFlags = new[] { PerimeterSealFlag },
            },
            new Scenario
            {
                name = "C4: board ready",
                description = "Case 4, hub clues collected and Override Clearance granted.",
                currentCase = 4, routeKey = "hub.4", collectCaseClues = true,
                setFlags = new[] { OverrideClearanceFlag },
            },
            new Scenario
            {
                name = "Post-C4 backtrack (I.1 armed)",
                description = "Case 5, all interlude flags cleared → hub arrivals replay.",
                currentCase = 5, routeKey = "hub.1",
                clearFlags = InterludeFlags,
            },
            new Scenario
            {
                name = "Post-C4: interludes played",
                description = "Case 5, all interlude flags set → arrivals stay silent.",
                currentCase = 5, routeKey = "hub.1",
                setFlags = InterludeFlags,
            },
            new Scenario
            {
                name = "Mistrial retried (C1)",
                description = "Sets the per-case mistrial flag used by grading.",
                setFlags = new[] { "Session.MistrialRetried.1" },
            },
            new Scenario
            {
                name = "Strained scan used (C1)",
                description = "Sets the per-case strained-scan flag.",
                setFlags = new[] { "Session.StrainedScan.1" },
            },
        };

        /// <summary>Apply a scenario to the live session. Returns a status line.</summary>
        public static string Apply(Scenario s)
        {
            var core = CoreRuntime.Instance;
            if (core == null) return "No CoreRuntime — enter Play mode first.";
            var session = core.Session;

            if (s.resetSession) session.Reset();
            if (s.currentCase.HasValue) session.CurrentCase = s.currentCase.Value;
            string clueStatus = "";
            if (s.collectCaseClues && s.currentCase.HasValue)
            {
                var data = core.ResolveCase(s.currentCase.Value);
                if (data == null) return $"{s.name}: case {s.currentCase.Value} unresolved.";
                int collected = 0;
                if (data.clues != null)
                    foreach (var clue in data.clues)
                    {
                        if (clue == null || clue.type == ClueType.Latent ||
                            clue.isBacktrackingOnly) continue;
                        if (session.RegisterClue(clue)) collected++;
                    }
                clueStatus = $" {collected} case clues registered.";
            }
            if (s.setFlags != null)
                foreach (var f in s.setFlags) session.SetFlag(f);
            if (s.clearFlags != null)
                foreach (var f in s.clearFlags) session.SetFlag(f, false);

            if (!string.IsNullOrEmpty(s.routeKey))
            {
                var scene = DevTravel.Resolve(s.routeKey);
                if (scene == null) return $"{s.name}: applied, route '{s.routeKey}' unregistered.";
                DevTravel.Go(scene);
            }
            return $"{s.name}: applied.{clueStatus}";
        }
    }
}
