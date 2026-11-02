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

        [MenuItem("Morsoul/Data/Generate Case 2 Assets")]
        static void BuildCase02()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Generate Morsoul Case Assets",
                "This will (re)generate the Case 2 data assets under Assets/Data/Cases/Case02, " +
                "overwriting all field values on any existing assets. Continue?",
                "Generate",
                "Cancel");
            if (!proceed) return;

            GenerateCase2Assets("Assets/Data/Cases");
            ValidateAllCases();
        }

        public static void GenerateCase3Assets(string rootFolder)
        {
            EnsureFolder(rootFolder);
            GenerateCase3(rootFolder + "/Case03");
            AssetDatabase.SaveAssets();
            // See Generate(): refresh so freshly created assets import before load.
            AssetDatabase.Refresh();
        }

        [MenuItem("Morsoul/Data/Generate Case 3 Assets")]
        static void BuildCase03()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Generate Morsoul Case Assets",
                "This will (re)generate the Case 3 data assets under Assets/Data/Cases/Case03, " +
                "overwriting all field values on any existing assets. Continue?",
                "Generate",
                "Cancel");
            if (!proceed) return;

            GenerateCase3Assets("Assets/Data/Cases");
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

        public static void GenerateCase2Assets(string rootFolder)
        {
            EnsureFolder(rootFolder);
            GenerateCase2(rootFolder + "/Case02");
            AssetDatabase.SaveAssets();
            // See Generate(): refresh so freshly created assets import before load.
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

        static void GenerateCase2(string folder)
        {
            EnsureFolder(folder);

            var c1 = Clue(folder, "C1_02", "Fractured Resonance Crystal", ClueType.Physical, 2,
                AcquisitionMethod.Raycast3D, Tool.None,
                "Shards of an acoustic focus crystal shattered directly beneath the Great Bell. Impact shear patterns indicate internal harmonic overload rather than physical blunt trauma.",
                hasWorldPosition: true, worldPosition: new Vector2(-4, 4));
            var c2 = Clue(folder, "C2_02", "Dark Mana Dust on Clapper", ClueType.Physical, 2,
                AcquisitionMethod.GolemEyeScan, Tool.GolemEye,
                "Black metallic soot adhering to the bell clapper's strike face. Golem Eye scan reveals an unnatural frequency absorption field: it converts kinetic strikes into an acoustic muting shockwave.",
                hasWorldPosition: true, worldPosition: new Vector2(4, 4));
            var c3 = Clue(folder, "C3_02", "Blackmailer's Coercion Note", ClueType.Physical, 2,
                AcquisitionMethod.Raycast3D, Tool.None,
                "A crumpled letter hidden behind the timber scaffolding addressed to Accord, sealed with the Veiled Circle's veil sigil (later recognized on Vane's ritual dagger in Case 3). Demands the application of the \"damping powder\" before the vesper chime, threatening Accord's sister Marian \u2014 her medical debt called in full and her sanctuary asylum revoked.",
                hasWorldPosition: true, worldPosition: new Vector2(0, 4));
            var t1 = Clue(folder, "T1_02", "Acolyte's Plea & Alibi", ClueType.Testimony, 2,
                AcquisitionMethod.Dialogue2D, Tool.None,
                "I was tending the courtyard lanterns under Brother Kenneth's eye until the bell tolled. My spirits sing with nature, not with cursed muting dust!",
                dissonanceKeyword: "spirits sing with nature");
            var t2 = Clue(folder, "T2_02", "Accord's Accusation", ClueType.Testimony, 2,
                AcquisitionMethod.Dialogue2D, Tool.None,
                "I saw the spirit-folk chanting near the tower base right before the chime! Their foreign sorcery shattered the crystal and stole our Mother's voice!",
                dissonanceKeyword: "chanted from the tower base");
            var rh1 = Clue(folder, "RH1_02", "Ritual Cleansing Tuning Fork", ClueType.RedHerring, 2,
                AcquisitionMethod.Inspection2D, Tool.None,
                "An ornate platinum tuning fork belonging to Lyra, found vibrating faintly with holy mana. A standard devotional instrument used for pitch alignment, bearing zero dark resonance traces.");
            var l2 = Clue(folder, "L2_02", "Runic Log #2: Acoustic Stabilization", ClueType.Latent, 2,
                AcquisitionMethod.AetherSight, Tool.AetherSight,
                "Pre-cataclysm architectural rune etched into the tower's rear load-bearing masonry beam. Documents harmonic acoustic dissipation used to stabilize runaway mana resonance. Required for Ending 3; unobtainable on initial run.",
                isBacktrackingOnly: true, hasWorldPosition: true, worldPosition: new Vector2(0, 8));

            var p1 = Premise(folder, "P1_02", "Acoustic Resonance Sabotage",
                "The silence hex was an acoustic trap delivered through the Great Bell's vibration, not an external spell cast from the ground.");
            var p2 = Premise(folder, "P2_02", "Internal Extortion & Scapegoating",
                "The bell-ringer acted under coercion from a third party, attempting to pin blame on the vulnerable spirit-folk outsider.");

            var in1 = Node(folder, "IN1_02", c1, c2,
                Prompt(
                    "The crystal shattered from internal harmonic overload, and the clapper carries dust that turns kinetic strikes into a muting shockwave. What does this establish?",
                    "An external impact cracked the crystal and the dust is unrelated residue.",
                    "The tuning fork's holy resonance destabilized the crystal from within.",
                    "Someone treated the clapper with a frequency-absorbing agent \u2014 sabotage meant to strangle the chime.",
                    2,
                    "Your scan reads absorption, not blunt force. Something was applied to that clapper to silence the chime \u2014 and the overload is what burst the crystal."),
                p1,
                new[] { rh1 },
                "Physical Contradiction: Harmonic signature is holy resonance, incapable of producing dark frequency absorption.");
            var in2 = Node(folder, "IN2_02", c3, t1,
                Prompt(
                    "A Veiled Circle note orders Accord to apply the 'damping powder' or lose his sister \u2014 while Sylvan's alibi holds under Brother Kenneth's watch. What follows?",
                    "Sylvan's spirit-folk chanting sabotaged the bell from the tower base.",
                    "Accord applied the powder under extortion \u2014 and pointed the blade at a convenient Acolyte.",
                    "The Veiled Circle entered the belfry and dusted the clapper themselves.",
                    1,
                    "The note names no perpetrator \u2014 it names a debt and a deadline. Someone with access applied that powder. Sylvan has a witness. Accord has a motive."),
                p2,
                new ClueData[0], "");

            var interrogation = LoadOrCreate<InterrogationData>($"{folder}/INT_02.asset");
            interrogation.id = "INT_02";
            interrogation.suspectName = "Accord the Bell-Ringer";
            interrogation.composureSegments = 3;
            interrogation.falsehoodShieldActive = true;
            interrogation.phases = new[]
            {
                new InterrogationPhase
                {
                    suspectClaim = "Why interrogate me when the culprit sits in chains? The spirit-folk's wild spirit magic cursed our High Priestess to desecrate our holy sanctuary!",
                    keywords = new[] { "wild spirit magic" },
                    validMoves = new[]
                    {
                        Move(Verb.Press, null, false, 0, false,
                            "Their kind can't control pure light! The second that heathen looked up, the air felt suffocating and the Lady lost her voice!"),
                        Move(Verb.Contradict, c2, true, 0, true,
                            "The muting wasn't cast through prayer or wild spirit arts. The clapper itself was prepared with synthetic dark soot that pulsed when struck."),
                    },
                    invalidMoves = new MoveData[0],
                },
                new InterrogationPhase
                {
                    suspectClaim = "Then the creature must have snuck up while I wasn't looking! I saw them, I swear \u2014 they chanted from the tower base and bewitched the machinery!",
                    keywords = new string[0],
                    validMoves = new[]
                    {
                        Move(Verb.PresentEvidence, p1, true, 0, false,
                            "The acoustics substantiate a redirect: a spell from the base cannot coat a clapper suspended eighty feet in the rafters. The damping agent was applied manually by whoever climbed the belfry before vespers."),
                    },
                    invalidMoves = new[]
                    {
                        Move(Verb.PresentEvidence, rh1, false, 1, false,
                            "The High Priestess's holy fork? That proves her sanctity, not my guilt! Are your cognitive processors rusted?!"),
                    },
                },
                new InterrogationPhase
                {
                    suspectClaim = "I am a faithful servant of Saint Althea! I have dedicated twenty years to ringing these bells! What possible motive could I have to harm Mother Lyra?!",
                    keywords = new string[0],
                    validMoves = new[]
                    {
                        Move(Verb.Contradict, c3, true, 0, false,
                            "You didn't want to harm her, Accord. You were cornered. The hooded stranger held your family's exile over your head."),
                    },
                    invalidMoves = new MoveData[0],
                },
            };
            EditorUtility.SetDirty(interrogation);

            var h = LoadOrCreate<HypothesisData>($"{folder}/H_02.asset");
            h.id = "H_02";
            h.premise1 = p1;
            h.premise2 = p2;
            h.verdictProposition = "Accord poisoned the clapper under blackmail from the Veiled Circle, falsely framing the Spirit-folk Acolyte Sylvan to shield his sister Marian.";
            h.unlocksInterrogation = interrogation;
            EditorUtility.SetDirty(h);

            var log = LoadOrCreate<RunicLogData>($"{folder}/RunicLog_02.asset");
            log.id = 2;
            log.caseId = 2;
            log.location = "Belfry \u2014 rear foundation beam behind the bell housing (0, 8)";
            log.requiredTool = Tool.AetherSight;
            log.contents = "Harmonic Acoustic Stabilization \u2014 harmonic acoustic dissipation used to stabilize runaway mana resonance.";
            log.availableAfterCase = 4;
            EditorUtility.SetDirty(log);

            var caseData = LoadOrCreate<CaseData>($"{folder}/Case_02.asset");
            caseData.caseId = 2;
            caseData.title = "The Muted Belfry of Saint Althea";
            caseData.region = "Cathedral Nave & Tower Belfry of Saint Althea, Asterveil";
            caseData.inquiryNodes = new[] { in1, in2 };
            caseData.hypothesis = h;
            caseData.interrogation = interrogation;
            caseData.clues = new[] { c1, c2, c3, t1, t2, rh1, l2 };
            caseData.latentLog = log;
            caseData.toolUnlocked = Tool.None;
            caseData.uneaseFlagKey = "Case2_UneaseActive";
            caseData.hasInvestigationScene = true;
            caseData.hasInterrogation = true;
            caseData.preloadedClues = new ClueData[0];
            EditorUtility.SetDirty(caseData);
        }

        static void GenerateCase3(string folder)
        {
            EnsureFolder(folder);

            var c1 = Clue(folder, "C1_03", "Brass Extraction Siphon", ClueType.Physical, 3,
                AcquisitionMethod.Raycast3D, Tool.None,
                "Industrial pneumatic tap driven deep into the primary heartwood taproot. Golem Eye inspection confirms high-pressure, forced sap drainage channeling raw environmental mana into pressurized canisters.",
                hasWorldPosition: true, worldPosition: new Vector2(0, 8));
            var c2 = Clue(folder, "C2_03", "Inscribed Silver Ritual Dagger", ClueType.Physical, 3,
                AcquisitionMethod.Raycast3D, Tool.None,
                "Ceremonial silver dagger bearing the stylized eclipsed eye insignia of the Veiled Circle. The blade edge is chemically coated with the same solvent volatility stabilizer identified in Case 1.",
                hasWorldPosition: true, worldPosition: new Vector2(-4, 0));
            var c3 = Clue(folder, "C3_03", "Vane's Discarded Ritual Cloak", ClueType.Physical, 3,
                AcquisitionMethod.EmotionEcho, Tool.EmotionEcho,
                "Vane's discarded ritual cloak at the altar; read with the newly unlocked Emotion Echo, revealing lingering residue of fanatical zeal and deceit that ties him to the Veiled Circle's Sanctum Core ritual.",
                hasWorldPosition: true, worldPosition: new Vector2(-4, 4));
            var t1 = Clue(folder, "T1_03", "Ranger Scout's Report", ClueType.Testimony, 3,
                AcquisitionMethod.Dialogue2D, Tool.None,
                "The rot began abruptly five nights ago, radiating from the altar clearing. No natural disease drains vital sap that fast without leaving fungal rot.",
                dissonanceKeyword: "natural forest blight");
            var t2 = Clue(folder, "T2_03", "Vane's Fanatical Justification", ClueType.Testimony, 3,
                AcquisitionMethod.Dialogue2D, Tool.None,
                "I am merely a wandering botanical researcher studying natural forest blight. You meddling guild hounds are disturbing a sanctioned ecological survey!",
                dissonanceKeyword: "sanctioned ecological survey");
            var rh1 = Clue(folder, "RH1_03", "Blighted Bark Shavings", ClueType.RedHerring, 3,
                AcquisitionMethod.Inspection2D, Tool.None,
                "Stripped bark infested with common wood-boring beetles. A natural secondary infestation attracted by the tree's dying state, entirely unrelated to the deep mana drainage.");
            var l3 = Clue(folder, "L3_03", "Runic Log #3: Displacement Device Telemetry", ClueType.Latent, 3,
                AcquisitionMethod.AetherSight, Tool.AetherSight,
                "Available only after Case 4, when Aether Sight is unlocked. Decrypted pre-cataclysm technical readout recording the catastrophic mana displacement event. Contains Elise Cochran's final transmission before the world reshaped, revealing the first tangible proof of her sacrifice.",
                isBacktrackingOnly: true, hasWorldPosition: true, worldPosition: new Vector2(0, 4));

            var p1 = Premise(folder, "P1_03", "Forced Industrial Extraction",
                "The Elder Tree is dying from mechanical high-pressure mana drainage, definitively disproving any claim of natural biological blight.");
            var p2 = Premise(folder, "P2_03", "Cult Conspiracy Link",
                "The drainage is orchestrated by the Veiled Circle; the emotional residue of fanatical zeal and deceit on Vane's cloak ties him to the cult's Sanctum Core ritual.");

            var in1 = Node(folder, "IN1_03", c1, t1,
                Prompt(
                    "The tube driven into the Elder Tree's taproot is an industrial high-pressure siphon, and Kaelen reports the blight radiated from the altar clearing with no fungal rot. What does this establish?",
                    "A natural forest blight is draining the tree \u2014 the brass device is unrelated research equipment.",
                    "A territorial monster wounded the tree, and the siphon was installed by the rangers to treat it.",
                    "Someone is mechanically draining the tree's mana through an industrial siphon \u2014 no natural blight works this fast or this clean.",
                    2,
                    "Kaelen said it himself: no disease drains vital sap that fast without leaving fungal rot. That tap isn't studying the tree \u2014 it's bleeding it."),
                p1,
                new[] { rh1 },
                "Forensic Contradiction: Beetle infestation is a superficial byproduct; root sap pressures show active mechanical drainage.");
            var in2 = Node(folder, "IN2_03", c2, c3,
                Prompt(
                    "The altar dagger bears the Veiled Circle's eclipsed eye, and the abandoned cloak still carries fanatical zeal and deceit under the Emotion Echo. What follows?",
                    "The dagger and cloak are curios looted by a lone black-market merchant with no ties to the drainage.",
                    "The drainage is a Veiled Circle operation \u2014 the zeal and deceit soaked into Vane's cloak tie him to their Sanctum Core ritual.",
                    "A lone sap smuggler planted cult trappings at the altar to pin his siphoning on the Veiled Circle.",
                    1,
                    "Insignia and residue tell one story: the eclipsed eye marks the blade, and the Echo reads zeal and deceit on the cloak. This was no merchant's commerce \u2014 it was a cult operation."),
                p2,
                new ClueData[0], "");

            var pressMove = Move(Verb.Press, null, false, 0, false,
                "The regional council granted me unrestricted access to sample fungal spread! My tools are delicate biological instruments, not weapons!");
            pressMove.emotionAura = Aura.Amber;

            var interrogation = LoadOrCreate<InterrogationData>($"{folder}/INT_03.asset");
            interrogation.id = "INT_03";
            interrogation.suspectName = "Vane the Veiled Circle Operative";
            interrogation.composureSegments = 3;
            interrogation.falsehoodShieldActive = true;
            interrogation.phases = new[]
            {
                new InterrogationPhase
                {
                    suspectClaim = "Stand down, guild relic! I am an accredited field scholar conducting a sanctioned ecological survey on natural forest blight!",
                    keywords = new[] { "sanctioned ecological survey" },
                    validMoves = new[]
                    {
                        pressMove,
                        Move(Verb.Contradict, c1, true, 0, true,
                            "Botanical scholars do not drive industrial pressurized pneumatic taps into ancient taproots. That apparatus is draining hundreds of liters of raw mana per hour."),
                    },
                    invalidMoves = new MoveData[0],
                },
                new InterrogationPhase
                {
                    suspectClaim = "Fine, I tapped the tree for alchemical commerce! But a lone merchant selling sap on the black market hardly concerns your guild's grand conspiracy theories!",
                    keywords = new string[0],
                    validMoves = new[]
                    {
                        Move(Verb.PresentEvidence, c2, true, 0, false,
                            "This redirects your 'lone merchant' story. Black-market traders do not carry daggers bearing the eclipsed eye of the Veiled Circle, treated with the exact same chemical solvent used in Asterveil's vault. Who do you actually work for?"),
                    },
                    invalidMoves = new[]
                    {
                        Move(Verb.PresentEvidence, rh1, false, 1, false,
                            "Rotten timber? Are you going to indict me for beetle droppings next, machine? Your cognitive core is failing you."),
                    },
                },
                new InterrogationPhase
                {
                    suspectClaim = "You cannot stop what is coming! You have no proof this tree connects to anything beyond these miserable woods!",
                    keywords = new string[0],
                    validMoves = new[]
                    {
                        Move(Verb.Contradict, c3, true, 0, false,
                            "Your own cloak betrays you, Vane \u2014 the Emotion Echo reads the fanatical zeal and deceit soaked into it. You serve the Veiled Circle's Sanctum Core ritual, and this tree feeds it."),
                    },
                    invalidMoves = new MoveData[0],
                },
            };
            EditorUtility.SetDirty(interrogation);

            var h = LoadOrCreate<HypothesisData>($"{folder}/H_03.asset");
            h.id = "H_03";
            h.premise1 = p1;
            h.premise2 = p2;
            h.verdictProposition = "Vane is an undercover operative of the Veiled Circle actively draining the Elder Spirit Tree to power their catastrophic core ritual.";
            h.unlocksInterrogation = interrogation;
            EditorUtility.SetDirty(h);

            var log = LoadOrCreate<RunicLogData>($"{folder}/RunicLog_03.asset");
            log.id = 3;
            log.caseId = 3;
            log.location = "Deep Grove Clearing \u2014 central ritual altar ground (0, 4)";
            log.requiredTool = Tool.AetherSight;
            log.contents = "Displacement Device Telemetry \u2014 pre-cataclysm technical readout recording the catastrophic mana displacement event and Elise Cochran's final transmission.";
            log.availableAfterCase = 4;
            EditorUtility.SetDirty(log);

            var caseData = LoadOrCreate<CaseData>($"{folder}/Case_03.asset");
            caseData.caseId = 3;
            caseData.title = "The Sinking Wilds of Whisperwood";
            caseData.region = "Whisperwood Boundary Trail & Deep Grove Clearing";
            caseData.inquiryNodes = new[] { in1, in2 };
            caseData.hypothesis = h;
            caseData.interrogation = interrogation;
            caseData.clues = new[] { c1, c2, c3, t1, t2, rh1, l3 };
            caseData.latentLog = log;
            caseData.toolUnlocked = Tool.EmotionEcho;
            caseData.uneaseFlagKey = "Case3_UneaseActive";
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
