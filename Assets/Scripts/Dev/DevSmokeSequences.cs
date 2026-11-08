using System.Collections;
using System.Collections.Generic;
using Morsoul.Core;
using Morsoul.Hub;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Morsoul.Dev
{
    /// <summary>
    /// Scripted semi-automated smoke runs for the dev overlay. Each sequence
    /// is a coroutine that drives real scene loads and serialized checks,
    /// reporting PASS/FAIL lines through <see cref="DevReport"/>.
    /// </summary>
    public static class DevSmokeSequences
    {
        public class Sequence
        {
            public string name;
            public string description;
            public System.Func<DevReport, IEnumerator> run;
        }

        /// <summary>Collects PASS/FAIL output from a running sequence.</summary>
        public class DevReport
        {
            public readonly List<string> lines = new List<string>();
            public int passCount;
            public int failCount;
            public bool done;

            public void Check(bool ok, string label)
            {
                if (ok) { passCount++; lines.Add($"PASS  {label}"); }
                else { failCount++; lines.Add($"FAIL  {label}"); }
            }

            public void Note(string text) => lines.Add($"--    {text}");
        }

        public static readonly List<Sequence> All = new List<Sequence>
        {
            new Sequence
            {
                name = "Hub integrity sweep",
                description = "Loads each registered hub and asserts the standard " +
                              "skeleton: context, player, carrier, CaseDoor, travel " +
                              "menu, HUD, board panel, gate.",
                run = HubIntegritySweep,
            },
            new Sequence
            {
                name = "Travel destination sweep",
                description = "In each hub, verifies the carrier menu enumerates " +
                              "exactly the unlocked hubs minus the current one.",
                run = TravelSweep,
            },
            new Sequence
            {
                name = "Session round-trip",
                description = "No scene loads: exercises flag/clue/case writes and " +
                              "checkpoint stamping on the live session.",
                run = SessionRoundTrip,
            },
        };

        // --- Sequences --------------------------------------------------------

        static IEnumerator HubIntegritySweep(DevReport r)
        {
            if (CoreRuntime.Instance == null)
            {
                r.Note("No CoreRuntime — start Play from Title first.");
                yield break;
            }
            for (int caseId = 1; caseId <= 4; caseId++)
            {
                var scene = DevTravel.Resolve(RouteKeys.Hub(caseId));
                if (scene == null)
                {
                    r.Note($"hub.{caseId}: route unregistered, skipped.");
                    continue;
                }
                if (!Application.CanStreamedLevelBeLoaded(scene))
                {
                    r.Note($"hub.{caseId} -> '{scene}': not in build list, skipped.");
                    continue;
                }
                r.Note($"--- hub.{caseId} ({scene}) ---");
                DevTravel.Go(scene);
                yield return WaitForScene(scene, r);
                if (!SceneIs(scene)) continue;

                var ctx = Object.FindAnyObjectByType<HubContext>();
                r.Check(ctx != null, "HubContext present");
                r.Check(Object.FindAnyObjectByType<HubPlayerController>() != null,
                    "HubPlayerController present");
                r.Check(Object.FindAnyObjectByType<CameraFollow2D>() != null,
                    "CameraFollow2D present");
                r.Check(GameObject.Find("Carrier") != null, "Carrier object present");
                r.Check(GameObject.Find("CaseDoor") != null, "CaseDoor object present");
                r.Check(GameObject.Find("RestSpot") != null, "RestSpot object present");
                r.Check(GameObject.Find("WeightedBoardPanel") != null,
                    "WeightedBoardPanel present");
                r.Check(ctx != null && ctx.travelMenu != null,
                    "TravelMenuController wired");
                r.Check(Object.FindAnyObjectByType<HubHudController>() != null,
                    "HubHudController present");
                r.Check(Object.FindObjectsByType<GateInteractable>()
                        .Length > 0, "At least one gate present");
                r.Check(Object.FindObjectsByType<NpcInteractable>()
                        .Length > 0, "At least one NPC present");
                if (ctx != null && ctx.flags != null)
                {
                    var gate = FirstGate(ctx);
                    if (gate != null)
                        r.Check(!string.IsNullOrEmpty(gate.requiredFlag) ||
                                gate.name == "TrailExit",
                            $"Gate '{gate.name}' declares a requiredFlag");
                }
            }
        }

        static IEnumerator TravelSweep(DevReport r)
        {
            var core = CoreRuntime.Instance;
            if (core == null) { r.Note("No CoreRuntime."); yield break; }

            var savedCase = core.Session.CurrentCase;
            core.Session.CurrentCase = 5; // unlock everything
            try
            {
                for (int caseId = 1; caseId <= 4; caseId++)
                {
                    var scene = DevTravel.Resolve(RouteKeys.Hub(caseId));
                    if (scene == null || !Application.CanStreamedLevelBeLoaded(scene))
                    {
                        r.Note($"hub.{caseId}: unreachable, skipped.");
                        continue;
                    }
                    DevTravel.Go(scene);
                    yield return WaitForScene(scene, r);
                    if (!SceneIs(scene)) continue;

                    var ctx = Object.FindAnyObjectByType<HubContext>();
                    if (ctx == null || ctx.travelMenu == null)
                    {
                        r.Check(false, $"hub.{caseId}: travel menu unavailable");
                        continue;
                    }
                    var destinations = ctx.travelMenu
                        .CollectDestinations(SceneManager.GetActiveScene().name);
                    r.Check(destinations.Count == 3,
                        $"hub.{caseId}: {destinations.Count}/3 other hubs offered");
                    bool labelled = true;
                    foreach (var d in destinations)
                        if (string.IsNullOrEmpty(d.label)) labelled = false;
                    r.Check(labelled, $"hub.{caseId}: all destinations labelled");
                }
            }
            finally
            {
                core.Session.CurrentCase = savedCase;
            }
        }

        static IEnumerator SessionRoundTrip(DevReport r)
        {
            var core = CoreRuntime.Instance;
            if (core == null) { r.Note("No CoreRuntime."); yield break; }
            var s = core.Session;

            const string probe = "DevMenu.ProbeFlag";
            s.SetFlag(probe);
            r.Check(s.HasFlag(probe), "flag set readable");
            s.SetFlag(probe, false);
            r.Check(!s.HasFlag(probe), "flag clear readable");

            var clue = core.ResolveClue("T1_01");
            r.Check(clue != null, "ResolveClue('T1_01')");
            if (clue != null)
            {
                r.Check(s.RegisterClue(clue), "RegisterClue(T1_01)");
                r.Check(s.HasClue("T1_01"), "HasClue(T1_01)");
            }

            int before = s.CurrentCase;
            s.CurrentCase = before + 1;
            r.Check(s.CurrentCase == before + 1, "CurrentCase writable");
            s.CurrentCase = before;

            var gsm = GameStateManager.Instance;
            if (gsm != null)
            {
                gsm.WriteCheckpoint("DevMenu", SceneManager.GetActiveScene().name);
                r.Check(gsm.CurrentSave != null, "checkpoint written");
            }
            yield return null;
        }

        // --- Helpers ----------------------------------------------------------

        static IEnumerator WaitForScene(string scene, DevReport r, float timeout = 15f)
        {
            float elapsed = 0f;
            while (!SceneIs(scene) && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            r.Check(SceneIs(scene), $"scene '{scene}' loaded within {timeout:0}s");
        }

        static bool SceneIs(string scene) =>
            SceneManager.GetActiveScene().name == scene;

        static GateInteractable FirstGate(HubContext ctx)
        {
            var gates = Object.FindObjectsByType<GateInteractable>();
            return gates.Length > 0 ? gates[0] : null;
        }
    }
}
