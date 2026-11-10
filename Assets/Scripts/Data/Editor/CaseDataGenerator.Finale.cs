using UnityEditor;
using UnityEngine;

namespace Morsoul.Data.Editor
{
    public static partial class CaseDataGenerator
    {
        [MenuItem("Morsoul/Data/Generate Case 4 + 5 Assets")]
        static void GenerateFinaleFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Generate Morsoul Case Assets",
                    "Generate Case 4 and 5 under Assets/Data/Cases? Existing field values will be overwritten.",
                    "Generate", "Cancel")) return;
            GenerateFinaleAssets("Assets/Data/Cases");
            ValidateAllCases();
        }

        public static void GenerateMissingFinaleAssets()
        {
            const string root = "Assets/Data/Cases";
            if (AssetDatabase.IsValidFolder(root + "/Case04") ||
                AssetDatabase.IsValidFolder(root + "/Case05"))
                throw new System.InvalidOperationException("Finale folders already exist. Use the confirmed editor menu to regenerate.");
            GenerateFinaleAssets(root);
            ValidateAllCases();
        }

        public static void GenerateFinaleAssets(string rootFolder)
        {
            EnsureFolder(rootFolder);
            GenerateCase4(rootFolder + "/Case04");
            GenerateCase5(rootFolder + "/Case05");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void GenerateCase4(string folder)
        {
            EnsureFolder(folder);
            var c1 = Clue(folder, "C1_04", "Tampered Pneumatic Valves", ClueType.Physical, 4,
                AcquisitionMethod.Raycast3D, Tool.None,
                "High-pressure pneumatic release valves manually wrenched past safety tolerances and jammed with iron wedge pins. Backpressure was intentionally forced to trigger a pipeline rupture.");
            var c2 = Clue(folder, "C2_04", "Chemically Dissolved Bypass Conduits", ClueType.Physical, 4,
                AcquisitionMethod.GolemEyeScan, Tool.GolemEye,
                "Heavy copper-insulated control wiring corroded into slag. Spectral inspection confirms the same synthetic mana solvent identified in Ignatius' vault and Whisperwood.");
            var c3 = Clue(folder, "C3_04", "Pre-Cataclysm Sub-Lab Blast Door", ClueType.Physical, 4,
                AcquisitionMethod.Raycast3D, Tool.None,
                "A reinforced titanium-alloy blast door hidden behind blown-out rock strata. Its emergency lock was tampered with from outside to force stasis lockdown over the engineering bay.");
            var t1 = Clue(folder, "T1_04", "Mine Overseer's Work Log", ClueType.Testimony, 4,
                AcquisitionMethod.Dialogue2D, Tool.None,
                "Thrun: Chief Engineer Brokk is a master craftsman with thirty years in these shafts; he never skips valve depressurization before cycling the main lift.",
                dissonanceKeyword: "reckless operator error");
            var t2 = Clue(folder, "T2_04", "Infiltrator's Contractor Defense", ClueType.Testimony, 4,
                AcquisitionMethod.Dialogue2D, Tool.None,
                "I was contracted solely for structural bedrock reinforcement on the upper tier. I never touched the subterranean steam conduits or engineering bypasses!",
                dissonanceKeyword: "never touched the subterranean conduits");
            var rh1 = Clue(folder, "RH1_04", "Stress-Fractured Drill Bit", ClueType.RedHerring, 4,
                AcquisitionMethod.Inspection2D, Tool.None,
                "A snapped pneumatic drill head with heavy metal fatigue and abrasive wear. Ordinary wear, unrelated to chemically melted junction relays.");
            var l4 = Clue(folder, "L4_04", "Runic Log #4: Planetary Stabilizer Console Blueprints", ClueType.Latent, 4,
                AcquisitionMethod.AetherSightPlusGolemEye, Tool.AetherSight,
                "After Brokk is freed, use Aether Sight and Golem Eye at the unsealed sub-lab terminal. The schematics contain operational formulas, fail-safe frequencies and manual core override commands for the ancient stabilizer.",
                hasWorldPosition: true, worldPosition: new Vector2(0, 16));
            l4.requiredFlag = "Case4_BrokkFreed";
            var press = Move(Verb.Press, null, false, 0, false,
                "Brokk was rushing quota! He forgot the bypass sequence and the lines blew. Simple industrial accident!");
            press.emotionAura = Aura.Amber;
            var press2 = Move(Verb.Press, null, false, 0, false,
                "My crew stays above the 400-meter mark! We don't touch hydraulic copper or acid baths! If chemical rot dissolved those lines, talk to the refinery alchemists!");
            press2.emotionAura = Aura.Violet;
            var press3 = Move(Verb.Press, null, false, 0, false,
                "The earth is dead rock! Shattering a few granite boulders doesn't threaten your precious city! The Grand Overseer will purify this blighted subterranean filth!");
            press3.emotionAura = Aura.Violet;
            var solvent = Move(Verb.PresentEvidence, c2, true, 0, false,
                "The bypass cables were melted with the same synthetic mana solvent found in Ignatius' vault and Vane's camp. This is a wider operation. Who supplied you?");
            solvent.emotionAura = Aura.Violet;
            var interrogation = LoadOrCreate<InterrogationData>($"{folder}/INT_04.asset");
            interrogation.id = "INT_04";
            interrogation.suspectName = "Harlan, Veiled Circle Infiltrator";
            interrogation.composureSegments = 3;
            interrogation.falsehoodShieldActive = true;
            interrogation.phases = new[]
            {
                new InterrogationPhase
                {
                    suspectClaim = "You are barking up the wrong scaffold, metal head. The Dwarves pushed their steam equipment beyond safety margins. This disaster was entirely reckless operator error!",
                    keywords = new[] { "reckless operator error" },
                    validMoves = new[]
                    {
                        press,
                        Move(Verb.Contradict, c1, true, 0, true,
                            "Dwarven engineers do not accidentally hammer hardened iron wedge pins into pressure relief gears. The valves were locked to ensure a backflow explosion.")
                    },
                    invalidMoves = new MoveData[0]
                },
                new InterrogationPhase
                {
                    suspectClaim = "So someone jammed a valve. That proves nothing against me! I was contracted solely for upper-tier masonry and never touched the subterranean conduits!",
                    keywords = new[] { "never touched the subterranean conduits" },
                    validMoves = new[] { press2, solvent },
                    invalidMoves = new[]
                    {
                        Move(Verb.PresentEvidence, rh1, false, 1, false,
                            "A broken drill? Tools snap down here daily. Your stone skull's wound too tight, machine.")
                    }
                },
                new InterrogationPhase
                {
                    suspectClaim = "Why would anyone intentionally collapse a dead-end rock face?! What possible gain is there in blowing out useless granite?!",
                    keywords = new[] { "useless granite" },
                    validMoves = new[]
                    {
                        press3,
                        Move(Verb.Contradict, c3, true, 0, false,
                            "It wasn't a dead end. The rock concealed the blast doors of an ancient research facility. You caused the blowout to breach it for the Veiled Circle.")
                    },
                    invalidMoves = new MoveData[0]
                }
            };
            EditorUtility.SetDirty(interrogation);
            var log = LoadOrCreate<RunicLogData>($"{folder}/RunicLog_04.asset");
            log.id = 4;
            log.caseId = 4;
            log.location = "Iron Hollow sub-lab terminal (0, 16)";
            log.requiredTool = Tool.AetherSight;
            log.contents = "Planetary Stabilizer Console Blueprints: operational formulas, fail-safe frequencies and manual core override commands.";
            log.availableAfterCase = 4;
            EditorUtility.SetDirty(log);
            var data = LoadOrCreate<CaseData>($"{folder}/Case_04.asset");
            data.caseId = 4;
            data.title = "The Sub-Surface Sabotage of Iron Hollow";
            data.region = "Iron Hollow Mining Settlement & Subterranean Excavation Facility";
            data.interrogation = interrogation;
            data.clues = new[] { c1, c2, c3, t1, t2, rh1, l4 };
            data.latentLog = log;
            data.toolUnlocked = Tool.AetherSight;
            data.uneaseFlagKey = "Case4_UneaseActive";
            data.hasInvestigationScene = true;
            data.hasInterrogation = true;
            data.preloadedClues = new ClueData[0];
            data.evidenceGraph = LoadCanonicalGraph(
                "Assets/Data/Cases/Case04/EVG_04.asset");
            EditorUtility.SetDirty(data);
        }

        static void GenerateCase5(string folder)
        {
            EnsureFolder(folder);
            var c1 = Clue(folder, "C1_05", "Active Siphon Pylons", ClueType.Physical, 5,
                AcquisitionMethod.Automatic, Tool.None,
                "North and South industrial pylons route stolen mana from Cases 1 through 4 into the fissure barrier. Telemetry confirms inverted polarity designed to force a tectonic fracture.");
            var c2 = Clue(folder, "C2_05", "Overloaded Central Fissure", ClueType.Physical, 5,
                AcquisitionMethod.Automatic, Tool.None,
                "Critical containment pressure at the planetary breach makes manual calibration impossible without pre-cataclysm stabilizer equations.");
            var c3 = Clue(folder, "C3_05", "Grand Overseer's Cult Doctrine", ClueType.Physical, 5,
                AcquisitionMethod.Automatic, Tool.None,
                "The Overseer's unredacted treatise cites ancient scientific papers but strikes out findings that mana is natural planetary lifeblood, exposing his distortion of First Age history.");
            var t1 = Clue(folder, "T1_05", "Eliza's Tactical Readout", ClueType.Testimony, 5,
                AcquisitionMethod.Automatic, Tool.None,
                "{PlayerName}, the ritual circle isn't drawing energy from the earth. It's back-feeding harvested mana to break the ancient stabilizer locks! You must sever the pylons first!");
            var rh1 = Clue(folder, "RH1_05", "Arcing Discharge Grounding Rod", ClueType.RedHerring, 5,
                AcquisitionMethod.Automatic, Tool.None,
                "An improvised copper grounding cable that can ground minor sparks, but cannot divert raw terawatt-scale planetary mana discharges.");
            var data = LoadOrCreate<CaseData>($"{folder}/Case_05.asset");
            data.caseId = 5;
            data.title = "The Sanctum Core Finale";
            data.region = "Sanctum Core / Central Planetary Mana Fissure";
            data.interrogation = null;
            data.clues = new[] { c1, c2, c3, t1, rh1 };
            data.latentLog = null;
            data.toolUnlocked = Tool.None;
            data.uneaseFlagKey = "";
            data.hasInvestigationScene = false;
            data.hasInterrogation = false;
            data.preloadedClues = data.clues;
            data.evidenceGraph = LoadCanonicalGraph(
                "Assets/Data/Cases/Case05/EVG_05.asset");
            EditorUtility.SetDirty(data);
        }
    }
}
