using System.IO;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Data.Editor
{
    public static class CaseDataGenerator
    {
        [MenuItem("Morsoul/Data/Generate Case 0 + 1 Assets")]
        static void GenerateFromMenu()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Generate Morsoul Case Assets",
                "This will (re)generate the Case 0 and Case 1 data assets under Assets/Data/Cases, " +
                "overwriting all field values on any existing assets. Continue?",
                "Generate",
                "Cancel");
            if (!proceed) return;

            Generate("Assets/Data/Cases");
            ValidateAllCases();
        }

        [MenuItem("Morsoul/Data/Validate All Cases")]
        public static void ValidateAllCases()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:CaseData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var caseData = AssetDatabase.LoadAssetAtPath<CaseData>(path);
                if (caseData == null) continue;
                var errors = caseData.Validate();
                if (errors.Count == 0)
                    Debug.Log($"CaseData OK: {path}", caseData);
                else
                    foreach (string error in errors)
                        Debug.LogError($"{path}: {error}", caseData);
            }
        }

        public static void Generate(string rootFolder)
        {
            EnsureFolder(rootFolder);
            GenerateCase0(rootFolder + "/Case00");
            GenerateCase1(rootFolder + "/Case01");
            AssetDatabase.SaveAssets();
            // Newly created assets are not imported until a refresh; without this,
            // LoadAssetAtPath on a first run can return the in-memory object with
            // unsaved default field values (empty refs/arrays/flags).
            AssetDatabase.Refresh();
        }

        static void GenerateCase0(string folder)
        {
            EnsureFolder(folder);

            var c1 = Clue(folder, "C1_00", "Torn Desk Logs", ClueType.Physical, 0,
                AcquisitionMethod.GolemEyeScan, Tool.GolemEye,
                "Torn pre-cataclysm technical manifests scattered across a workstation. Scanning with Golem Eye (Tab) initiates linguistic deciphering, teaching the Golem the modern tongue and identifying the site as an unauthorized governmental mana research installation.");
            var c2 = Clue(folder, "C2_00", "Overloaded Capacitor Relay", ClueType.Physical, 0,
                AcquisitionMethod.Raycast3D, Tool.None,
                "Shattered glass vacuum tube surrounded by crystallized mana slag. Demonstrates that power to the primary hydraulic security blast door was severed during a forced emergency system overload.");
            var c3 = Clue(folder, "C3_00", "Ancient Chamber Key", ClueType.Physical, 0,
                AcquisitionMethod.Raycast3D, Tool.None,
                "An emergency manual override key discovered beneath a collapsed steel girder in the equipment wreckage. Its magnetic frequency matches the unpowered manual bypass port of the stone exit door.");
            var t1 = Clue(folder, "T1_00", "Internal Golem Diagnostic Log", ClueType.Testimony, 0,
                AcquisitionMethod.Automatic, Tool.None,
                "Chassis Integrity: 42%. Consciousness core reconstituted from temporal drift. Memory sectors 01\u201389: Unreadable/Wiped. Optical Translation Protocol: Online.",
                dissonanceKeyword: "memory sectors wiped");
            var rh1 = Clue(folder, "RH1_00", "Rusted Pipe Segment", ClueType.RedHerring, 0,
                AcquisitionMethod.Raycast3D, Tool.None,
                "A sheared section of cast-iron steam conduit. Heavily oxidized and structurally compromised; cannot be used as a lever to pry open the reinforced stone door.");

            var p1 = Premise(folder, "P1_00", "Unpowered Facility Lockdown",
                "The facility suffered a catastrophic system overload, locking the main security doors in an unpowered failsafe state.");
            var p2 = Premise(folder, "P2_00", "Mechanical Bypass Available",
                "The Golem's operational analytical sensors can calibrate the salvaged emergency key into the unpowered manual lock port.");

            var in1 = Node(folder, "IN1_00", c1, c2,
                Prompt(
                    "The logs mark this place as a mana research installation, and the door's power relay burned out in an emergency overload. What does this establish?",
                    "Someone sealed the doors from outside to trap whoever remained.",
                    "A catastrophic overload cut the power, leaving the security doors locked in an unpowered failsafe.",
                    "The doors are still powered and will open once the facility reboots.",
                    1,
                    "Diagnostic note: crystallized mana slag forms only during an overload. A door that has lost its power does not open. It holds in failsafe."),
                p1,
                new[] { rh1 },
                "Tutorial Guidance: Brute force cannot overcome hydraulic stone locks. Seek an authentic manual override.");
            var in2 = Node(folder, "IN2_00", c3, t1,
                Prompt(
                    "The key's magnetic frequency matches the door's manual bypass port, and your optical systems report online. What follows?",
                    "The key can only work once power is restored to the facility.",
                    "The key is a decoy, and the real exit lies elsewhere in the ruins.",
                    "Your working sensors can calibrate the salvaged key into the unpowered manual port.",
                    2,
                    "Diagnostic note: manual bypass ports draw no power. They need only a matching key and a precise hand. Both are present."),
                p2,
                new ClueData[0], "");

            var h = LoadOrCreate<HypothesisData>($"{folder}/H_00.asset");
            h.id = "H_00";
            h.premise1 = p1;
            h.premise2 = p2;
            h.verdictProposition = "The sealed stone exit can be manually unlocked by inserting the Ancient Chamber Key directly into the secondary hydraulic tumbler.";
            h.unlocksInterrogation = null;
            EditorUtility.SetDirty(h);

            var caseData = LoadOrCreate<CaseData>($"{folder}/Case_00.asset");
            caseData.caseId = 0;
            caseData.title = "Awakening in the Ashes";
            caseData.region = "Subterranean Research Facility Ruins & Wilderness Threshold";
            caseData.inquiryNodes = new[] { in1, in2 };
            caseData.hypothesis = h;
            caseData.interrogation = null;
            caseData.clues = new[] { c1, c2, c3, t1, rh1 };
            caseData.latentLog = null;
            caseData.toolUnlocked = Tool.None;
            caseData.uneaseFlagKey = "";
            caseData.hasInvestigationScene = true;
            caseData.hasInterrogation = false;
            caseData.preloadedClues = new ClueData[0];
            EditorUtility.SetDirty(caseData);
        }

        static void GenerateCase1(string folder)
        {
            EnsureFolder(folder);

            var c1 = Clue(folder, "C1_01", "Pristine Vault Lock", ClueType.Physical, 1,
                AcquisitionMethod.Raycast3D, Tool.GolemEye,
                "Heavy Dwarven multi-pin cylinder lock; tumblers and pins pristine with zero tool marks, shearing, or acid burns. Must have been opened with an authentic key.",
                hasWorldPosition: true, worldPosition: new Vector2(0, 4));
            var c2 = Clue(folder, "C2_01", "Vaporized Solvent Residue", ClueType.Physical, 1,
                AcquisitionMethod.GolemEyeScan, Tool.GolemEye,
                "Rapid-sublimating synthetic solvent residue releasing harmless purple vapor \u2014 not dark magic.",
                hasWorldPosition: true, worldPosition: new Vector2(4, 4));
            var c3 = Clue(folder, "C3_01", "Altered Cargo Ledger", ClueType.Physical, 1,
                AcquisitionMethod.Raycast3D, Tool.None,
                "Batch numbers for shipment #408 prove the potions were never manufactured.",
                hasWorldPosition: true, worldPosition: new Vector2(-4, 4));
            var t1 = Clue(folder, "T1_01", "Guildmaster's Key Registry", ClueType.Testimony, 1,
                AcquisitionMethod.Dialogue2D, Tool.None,
                "Only two physical keys exist for that chamber \u2014 Ignatius carries one, and the Guild Archive holds the duplicate under wax seal. The door was locked from the outside.",
                dissonanceKeyword: "locked from the outside");
            var t2 = Clue(folder, "T2_01", "Ignatius' Initial Claim", ClueType.Testimony, 1,
                AcquisitionMethod.Dialogue2D, Tool.None,
                "A rogue infiltrator breached my inner sanctum using volatile dark magic, destroying my entire life's work!",
                dissonanceKeyword: "volatile dark magic");
            var rh1 = Clue(folder, "RH1_01", "Bent Brass Lockpick", ClueType.RedHerring, 1,
                AcquisitionMethod.Inspection2D, Tool.None,
                "Cheap warped tension wrench; cannot engage a Dwarven vault cylinder.");
            var l1 = Clue(folder, "L1_01", "Runic Log #1: Solvent Volatility", ClueType.Latent, 1,
                AcquisitionMethod.AetherSight, Tool.AetherSight,
                "Pre-cataclysm formula on synthetic mana solvent flash points and sublimation rates. Required for Ending 3.",
                isBacktrackingOnly: true, hasWorldPosition: true, worldPosition: new Vector2(0, 8));

            var p1 = Premise(folder, "P1_01", "Absence of Forced Entry",
                "The vault was accessed without picking or damage, requiring one of only two registered keys.");
            var p2 = Premise(folder, "P2_01", "Fabricated Attack & Embezzlement",
                "The incident was chemically faked to claim insurance compensation for goods that were never brewed.");

            var in1 = Node(folder, "IN1_01", c1, t1,
                Prompt(
                    "The lock's pins are untouched, and only two registered keys exist. What does this establish?",
                    "The vault was opened with one of the two registered keys \u2014 no forced breach occurred.",
                    "A skilled thief picked the lock without leaving a single tool mark.",
                    "Dark magic bypassed the lock mechanism entirely.",
                    0,
                    "Dwarven cylinders scar if you so much as breathe on them with a pick. Clean pins, two keys... so who's carrying one?"),
                p1,
                new[] { rh1 },
                "Physical Contradiction: Tool profile does not match Dwarven tumbler specifications.");
            var in2 = Node(folder, "IN2_01", c2, c3,
                Prompt(
                    "The 'dark magic' residue is a mundane solvent, and shipment #408 was never brewed. What follows?",
                    "A rival alchemist sabotaged the vault to ruin Ignatius.",
                    "The attack was staged with a mundane solvent to claim insurance on potions that never existed.",
                    "The solvent leaked by accident and destroyed real stock.",
                    1,
                    "Funny thing about ghost cargo \u2014 you can't destroy what was never made. So who profits from a fake disaster?"),
                p2,
                new ClueData[0], "");

            var interrogation = LoadOrCreate<InterrogationData>($"{folder}/INT_01.asset");
            interrogation.id = "INT_01";
            interrogation.suspectName = "Master Alchemist Ignatius";
            interrogation.composureSegments = 3;
            interrogation.falsehoodShieldActive = true;
            interrogation.phases = new[]
            {
                new InterrogationPhase
                {
                    suspectClaim = "An unknown cabal breached my inner vault using volatile dark magic! You cannot hold me accountable for catastrophic supernatural sabotage!",
                    keywords = new[] { "volatile dark magic" },
                    validMoves = new[]
                    {
                        Move(Verb.Press, null, false, 0, false,
                            "The vapors were purple, acrid, and dissolved the vats in seconds! Only forbidden sorcery works that fast!"),
                        Move(Verb.Contradict, c2, true, 0, true,
                            "Spectral analysis detected synthetic solvent sublimation, not necromancy or dark magic. The smoke was a theatrical irritant."),
                    },
                    invalidMoves = new MoveData[0],
                },
                new InterrogationPhase
                {
                    suspectClaim = "Even if mundane solvent was used, some skilled cat burglar must have jimmied the locks from the back alley while Asterveil slept!",
                    keywords = new string[0],
                    validMoves = new[]
                    {
                        Move(Verb.PresentEvidence, p1, true, 0, false,
                            "The cylinder pins bear no scratches, and the Guild holds the only duplicate. That substantiates a single conclusion \u2014 the door was opened with a registered key, and only yours and the Guildmaster's exist. So whose key turned it?"),
                    },
                    invalidMoves = new[]
                    {
                        Move(Verb.PresentEvidence, rh1, false, 1, false,
                            "That crude wire? That wouldn't turn a tavern padlock, let alone dwarven masterwork steel! You are grasping at straws, automaton!"),
                    },
                },
                new InterrogationPhase
                {
                    suspectClaim = "This is absurd! Why would I destroy ten thousand gold ducats worth of inventory? I am financially ruined by this loss!",
                    keywords = new string[0],
                    validMoves = new[]
                    {
                        Move(Verb.Contradict, c3, true, 0, false,
                            "You destroyed nothing of value. Batch records confirm shipment #408 was never bottled. You staged an attack to collect guild insurance on phantom goods."),
                    },
                    invalidMoves = new MoveData[0],
                },
            };
            EditorUtility.SetDirty(interrogation);

            var h = LoadOrCreate<HypothesisData>($"{folder}/H_01.asset");
            h.id = "H_01";
            h.premise1 = p1;
            h.premise2 = p2;
            h.verdictProposition = "Master Alchemist Ignatius staged the break-in using his own key and mundane solvent to cover up inventory embezzlement.";
            h.unlocksInterrogation = interrogation;
            EditorUtility.SetDirty(h);

            var log = LoadOrCreate<RunicLogData>($"{folder}/RunicLog_01.asset");
            log.id = 1;
            log.caseId = 1;
            log.location = "Vault \u2014 sub-floor potion rack alcove (0, 8)";
            log.requiredTool = Tool.AetherSight;
            log.contents = "Mana Solvent Volatility \u2014 synthetic mana solvent flash points and sublimation rates.";
            log.availableAfterCase = 4;
            EditorUtility.SetDirty(log);

            var caseData = LoadOrCreate<CaseData>($"{folder}/Case_01.asset");
            caseData.caseId = 1;
            caseData.title = "The Poisoned Vault of Asterveil";
            caseData.region = "Guild Quarter & Lower Market District, Asterveil";
            caseData.inquiryNodes = new[] { in1, in2 };
            caseData.hypothesis = h;
            caseData.interrogation = interrogation;
            caseData.clues = new[] { c1, c2, c3, t1, t2, rh1, l1 };
            caseData.latentLog = log;
            caseData.toolUnlocked = Tool.None;
            caseData.uneaseFlagKey = "Case1_UneaseActive";
            caseData.hasInvestigationScene = true;
            caseData.hasInterrogation = true;
            caseData.preloadedClues = new ClueData[0];
            EditorUtility.SetDirty(caseData);
        }

        static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        static ClueData Clue(string folder, string id, string displayName, ClueType type, int caseId,
            AcquisitionMethod acquisition, Tool requiredTool, string description,
            string dissonanceKeyword = "", bool isBacktrackingOnly = false,
            bool hasWorldPosition = false, Vector2 worldPosition = default)
        {
            var clue = LoadOrCreate<ClueData>($"{folder}/{id}.asset");
            clue.id = id;
            clue.displayName = displayName;
            clue.type = type;
            clue.caseId = caseId;
            clue.acquisition = acquisition;
            clue.requiredTool = requiredTool;
            clue.description = description;
            clue.dissonanceKeyword = dissonanceKeyword;
            clue.isBacktrackingOnly = isBacktrackingOnly;
            clue.hasWorldPosition = hasWorldPosition;
            clue.worldPosition = worldPosition;
            EditorUtility.SetDirty(clue);
            return clue;
        }

        static PremiseData Premise(string folder, string id, string title, string proposition)
        {
            var premise = LoadOrCreate<PremiseData>($"{folder}/{id}.asset");
            premise.id = id;
            premise.title = title;
            premise.proposition = proposition;
            EditorUtility.SetDirty(premise);
            return premise;
        }

        static InquiryNodeData Node(string folder, string id, ClueData slotA, ClueData slotB,
            CausalPromptData prompt, PremiseData producesPremise,
            ClueData[] rejectedClues, string rejectionMessage)
        {
            var node = LoadOrCreate<InquiryNodeData>($"{folder}/{id}.asset");
            node.id = id;
            node.slotA = slotA;
            node.slotB = slotB;
            node.prompt = prompt;
            node.producesPremise = producesPremise;
            node.rejectedClues = rejectedClues;
            node.rejectionMessage = rejectionMessage;
            EditorUtility.SetDirty(node);
            return node;
        }

        static CausalPromptData Prompt(string question, string option0, string option1, string option2,
            int correctIndex, string hintText)
        {
            return new CausalPromptData
            {
                question = question,
                options = new[] { option0, option1, option2 },
                correctIndex = correctIndex,
                hintText = hintText,
            };
        }

        static MoveData Move(Verb verb, EvidenceData evidence, bool breaksComposureSegment,
            int strikeCost, bool isCoreContradiction, string responseDialogue)
        {
            return new MoveData
            {
                verb = verb,
                evidence = evidence,
                breaksComposureSegment = breaksComposureSegment,
                strikeCost = strikeCost,
                isCoreContradiction = isCoreContradiction,
                responseDialogue = responseDialogue,
                emotionAura = Aura.None,
            };
        }
    }
}
