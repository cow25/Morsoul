using System.IO;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Data.Editor
{
    public static partial class CaseDataGenerator
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
                "Torn modern-tongue notes scattered across a workstation, left by a human who studied these ruins long after the Cataclysm. Scanning with <b>Golem Eye (Tab)</b> initiates <b>linguistic deciphering</b>, teaching the Golem the modern tongue and identifying the site as a <b>sealed research installation</b>.");
            var c2 = Clue(folder, "C2_00", "Overloaded Capacitor Relay", ClueType.Physical, 0,
                AcquisitionMethod.Raycast3D, Tool.None,
                "Shattered glass vacuum tube surrounded by <b>crystallized mana slag</b>. Demonstrates that <b>power to the primary hydraulic blast door was severed</b> during a forced emergency system overload.");
            var c3 = Clue(folder, "C3_00", "Ancient Chamber Key", ClueType.Physical, 0,
                AcquisitionMethod.Raycast3D, Tool.None,
                "An <b>emergency manual override key</b> discovered beneath a collapsed steel girder in the equipment wreckage. Its <b>magnetic frequency</b> matches the <b>unpowered manual bypass port</b> of the stone exit door.");
            var t1 = Clue(folder, "T1_00", "Internal Golem Diagnostic Log", ClueType.Testimony, 0,
                AcquisitionMethod.Automatic, Tool.None,
                "Chassis Integrity: 42%. Consciousness core reconstituted from temporal drift. Memory sectors 01\u201389: Unreadable/Wiped. <b>Optical Translation Protocol: Online</b> — ready to decode unfamiliar scripts and calibrate manual ports.",
                dissonanceKeyword: "memory sectors wiped");
            var rh1 = Clue(folder, "RH1_00", "Rusted Pipe Segment", ClueType.RedHerring, 0,
                AcquisitionMethod.Raycast3D, Tool.None,
                "A sheared section of cast-iron steam conduit. <b>Heavily oxidized and structurally compromised</b>; cannot be used as a lever to pry open the reinforced stone door.");

            var caseData = LoadOrCreate<CaseData>($"{folder}/Case_00.asset");
            caseData.caseId = 0;
            caseData.title = "Awakening Beneath the Ruins";
            caseData.region = "Subterranean Research Facility Ruins & Wilderness Threshold";
            caseData.interrogation = null;
            caseData.clues = new[] { c1, c2, c3, t1, rh1 };
            caseData.latentLog = null;
            caseData.toolUnlocked = Tool.None;
            caseData.uneaseFlagKey = "";
            caseData.hasInvestigationScene = true;
            caseData.hasInterrogation = false;
            caseData.preloadedClues = new ClueData[0];
            caseData.evidenceGraph = LoadCanonicalGraph(
                "Assets/Data/Cases/Case00/EVG_00_PROVISIONAL.asset");
            EditorUtility.SetDirty(caseData);
        }

        static void GenerateCase1(string folder)
        {
            EnsureFolder(folder);

            var c1 = Clue(folder, "C1_01", "Pristine Vault Lock", ClueType.Physical, 1,
                AcquisitionMethod.Raycast3D, Tool.GolemEye,
                "Heavy Dwarven multi-pin cylinder lock; <b>tumblers and pins pristine</b> with <b>zero tool marks, shearing, or acid burns</b>. Must have been opened with an <b>authentic key</b>.",
                hasWorldPosition: true, worldPosition: new Vector2(0, 4));
            var c2 = Clue(folder, "C2_01", "Vaporized Solvent Residue", ClueType.Physical, 1,
                AcquisitionMethod.GolemEyeScan, Tool.GolemEye,
                "<b>Rapid-sublimating synthetic solvent residue</b> releasing harmless <b>purple vapor</b> \u2014 theatrical irritant, <b>not dark magic</b>.",
                hasWorldPosition: true, worldPosition: new Vector2(4, 4));
            var c3 = Clue(folder, "C3_01", "Altered Cargo Ledger", ClueType.Physical, 1,
                AcquisitionMethod.Raycast3D, Tool.None,
                "<b>Batch #408</b> entries were inked over blank manifests — proving the <b>ten-thousand-ducat potion stock was never manufactured</b>.",
                hasWorldPosition: true, worldPosition: new Vector2(-4, 4));
            var c4 = Clue(folder, "C4_01", "Draft Treasury Indemnity Claim", ClueType.Physical, 1,
                AcquisitionMethod.Raycast3D, Tool.None,
                "Pre-signed <b>Guild compensation claim for Batch #408</b>, dated <b>two days before the vault incident</b>, naming Ignatius as sole beneficiary.",
                hasWorldPosition: true, worldPosition: new Vector2(-2, 6));
            var c5 = Clue(folder, "C5_01", "Unmarked Solvent Crate", ClueType.Physical, 1,
                AcquisitionMethod.Inspection2D, Tool.None,
                "Stamped wooden crate stacked beside Ignatius's workshop smelling of <b>dried purple film</b>; wax-sealed with an <b>unlisted Veiled Circle supplier mark</b> matching the vault's <b>sublimating solvent</b>.");
            var t1 = Clue(folder, "T1_01", "Guildmaster's Key Registry", ClueType.Testimony, 1,
                AcquisitionMethod.Dialogue2D, Tool.None,
                "<b>Only two physical keys exist</b> for that chamber \u2014 <b>Ignatius carries one</b>, and the Guild Archive holds the duplicate under wax seal. The door was <b>locked from the outside</b>.",
                dissonanceKeyword: "locked from the outside");
            var t2 = Clue(folder, "T2_01", "Ignatius' Initial Claim", ClueType.Testimony, 1,
                AcquisitionMethod.Dialogue2D, Tool.None,
                "A rogue infiltrator breached my inner sanctum using <b>volatile dark magic</b>, destroying my entire life's work!",
                dissonanceKeyword: "volatile dark magic");
            var rh1 = Clue(folder, "RH1_01", "Bent Brass Lockpick", ClueType.RedHerring, 1,
                AcquisitionMethod.Inspection2D, Tool.None,
                "Cheap warped tension wrench; <b>cannot engage a Dwarven vault cylinder</b>.");
            var l1 = Clue(folder, "L1_01", "Runic Log #1: Solvent Volatility", ClueType.Latent, 1,
                AcquisitionMethod.AetherSight, Tool.AetherSight,
                "Pre-cataclysm formula on synthetic mana solvent flash points and sublimation rates. Required for Ending 3.",
                isBacktrackingOnly: true, hasWorldPosition: true, worldPosition: new Vector2(0, 8));

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
                        Move(Verb.Contradict, c5, true, 0, true,
                            "An unmarked crate stacked right outside your own workshop carries the exact same purple-film solvent. You didn't suffer a dark-magic ambush — you received a delivery."),
                    },
                    invalidMoves = new MoveData[0],
                },
                new InterrogationPhase
                {
                    suspectClaim = "Even if mundane solvent was used, some skilled cat burglar must have jimmied the locks from the back alley while Asterveil slept!",
                    keywords = new[] { "jimmied the locks from the back alley" },
                    validMoves = new[]
                    {
                        Move(Verb.Press, null, false, 0, false,
                            "I lock up strictly at the eighth bell! Any thief with a standard pick-set could slip past the sleepy market watch and force the cylinder!"),
                        Move(Verb.PresentEvidence, c1, true, 0, false,
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
                    keywords = new[] { "financially ruined by this loss" },
                    validMoves = new[]
                    {
                        Move(Verb.Press, null, false, 0, false,
                            "My entire life's work, my retirement, my family's trust fund was tied into Batch #408! If the City Treasury does not approve my emergency compensation by noon, I am bankrupt!"),
                        Move(Verb.Contradict, c3, true, 0, false,
                            "You destroyed nothing of value. Batch records confirm shipment #408 was never bottled. You staged an attack to collect guild insurance on phantom goods."),
                        Move(Verb.Contradict, c4, true, 0, false,
                            "You pre-signed a Treasury indemnity claim for Batch #408 two full days before the breach even occurred. You weren't ruined by an attack — you scheduled it."),
                    },
                    invalidMoves = new MoveData[0],
                },
            };
            EditorUtility.SetDirty(interrogation);

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
            caseData.interrogation = interrogation;
            caseData.clues = new[] { c1, c2, c3, c4, c5, t1, t2, rh1, l1 };
            caseData.latentLog = log;
            caseData.toolUnlocked = Tool.None;
            caseData.uneaseFlagKey = "Case1_UneaseActive";
            caseData.hasInvestigationScene = true;
            caseData.hasInterrogation = true;
            caseData.preloadedClues = new ClueData[0];
            caseData.evidenceGraph = LoadCanonicalGraph(
                "Assets/Data/Cases/Case01/EVG_01_PROVISIONAL.asset");
            EditorUtility.SetDirty(caseData);
        }

        static void GenerateCase2(string folder)
        {
            EnsureFolder(folder);

            var c1 = Clue(folder, "C1_02", "Fractured Resonance Crystal", ClueType.Physical, 2,
                AcquisitionMethod.Raycast3D, Tool.None,
                "Shards of an <b>acoustic focus crystal</b> shattered directly beneath the Great Bell. Shear patterns indicate <b>internal harmonic overload</b> rather than physical blunt trauma.",
                hasWorldPosition: true, worldPosition: new Vector2(-4, 4));
            var c2 = Clue(folder, "C2_02", "Dark Mana Dust on Clapper", ClueType.Physical, 2,
                AcquisitionMethod.GolemEyeScan, Tool.GolemEye,
                "<b>Black metallic damping soot</b> adhering to the bell clapper's strike face. Golem Eye scan reveals an <b>unnatural frequency absorption field</b> converting kinetic strikes into a <b>muting shockwave</b>.",
                hasWorldPosition: true, worldPosition: new Vector2(4, 4));
            var c3 = Clue(folder, "C3_02", "Blackmailer's Coercion Note", ClueType.Physical, 2,
                AcquisitionMethod.Raycast3D, Tool.None,
                "Crumpled letter hidden behind the scaffolding addressed to <b>Accord</b>, sealed with the <b>Veiled Circle's veil sigil</b>. Demands applying the <b>\"damping powder\"</b> before vespers, threatening Accord's sister <b>Marian's medical debt and sanctuary asylum</b>.",
                hasWorldPosition: true, worldPosition: new Vector2(0, 4));
            var c4 = Clue(folder, "C4_02", "Bell-Ringer's Soot-Stained Rag", ClueType.Physical, 2,
                AcquisitionMethod.Raycast3D, Tool.None,
                "Oily polishing cloth tucked into Accord's belfry bench, coated in the same <b>black metallic damping soot</b> found on the clapper — proving <b>manual application from inside the belfry</b>.",
                hasWorldPosition: true, worldPosition: new Vector2(2, 2));
            var c5 = Clue(folder, "C5_02", "Marian's Sanctuary Debt Notice", ClueType.Physical, 2,
                AcquisitionMethod.Inspection2D, Tool.None,
                "Stamped hospital ledger notice tucked near the Nave vestry pew: <b>Marian's sanctuary asylum debt</b> was bought out by an <b>anonymous veil-sealed creditor</b>, due at sunset unless Accord complies.");
            var t1 = Clue(folder, "T1_02", "Acolyte's Plea & Alibi", ClueType.Testimony, 2,
                AcquisitionMethod.Dialogue2D, Tool.None,
                "I was <b>tending the courtyard lanterns under Brother Kenneth's eye</b> until the bell tolled. My spirits sing with nature, not with cursed muting dust!",
                dissonanceKeyword: "spirits sing with nature");
            var t2 = Clue(folder, "T2_02", "Accord's Accusation", ClueType.Testimony, 2,
                AcquisitionMethod.Dialogue2D, Tool.None,
                "I saw the spirit-folk <b>chanting near the tower base</b> right before the chime! Their foreign sorcery shattered the crystal and stole our Mother's voice!",
                dissonanceKeyword: "chanted from the tower base");
            var rh1 = Clue(folder, "RH1_02", "Ritual Cleansing Tuning Fork", ClueType.RedHerring, 2,
                AcquisitionMethod.Inspection2D, Tool.None,
                "Ornate platinum tuning fork belonging to Lyra, vibrating faintly with holy mana. Standard devotional instrument bearing <b>zero dark resonance traces</b>.");
            var l2 = Clue(folder, "L2_02", "Runic Log #2: Acoustic Stabilization", ClueType.Latent, 2,
                AcquisitionMethod.AetherSight, Tool.AetherSight,
                "Pre-cataclysm architectural rune etched into the tower's rear load-bearing masonry beam. Documents harmonic acoustic dissipation used to stabilize runaway mana resonance. Required for Ending 3; unobtainable on initial run.",
                isBacktrackingOnly: true, hasWorldPosition: true, worldPosition: new Vector2(0, 8));

            var c2Press0 = Move(Verb.Press, null, false, 0, false,
                "Their kind can't control pure light! The second that heathen looked up, the air felt suffocating and the Lady lost her voice!");
            c2Press0.emotionAura = Aura.Amber;
            var c2Press1 = Move(Verb.Press, null, false, 0, false,
                "I was busy oiling the rope pulleys! A cunning sorcerer could whisper upward through the floor flues without making a sound!");
            c2Press1.emotionAura = Aura.Violet;
            var c2Press2 = Move(Verb.Press, null, false, 0, false,
                "I love this parish more than my own life! I would sever my own fingers before I willingly silenced the Lady! You cannot invent a reason for me to commit blasphemy!");
            c2Press2.emotionAura = Aura.Amber;

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
                        c2Press0,
                        Move(Verb.Contradict, c2, true, 0, true,
                            "The muting wasn't cast through prayer or wild spirit arts. The clapper itself was prepared with synthetic dark soot that pulsed when struck."),
                    },
                    invalidMoves = new MoveData[0],
                },
                new InterrogationPhase
                {
                    suspectClaim = "Then the creature must have snuck up while I wasn't looking! I saw them, I swear \u2014 they chanted from the tower base and bewitched the machinery!",
                    keywords = new[] { "chanted from the tower base" },
                    validMoves = new[]
                    {
                        c2Press1,
                        Move(Verb.PresentEvidence, c1, true, 0, false,
                            "The acoustics substantiate a redirect: a spell from the base cannot coat a clapper suspended eighty feet in the rafters. The damping agent was applied manually by whoever climbed the belfry before vespers."),
                        Move(Verb.PresentEvidence, c4, true, 0, false,
                            "Your own polishing rag from the belfry bench is saturated with the exact same black metallic damping soot. Nobody chanted it onto the clapper from the courtyard — you wiped it on by hand."),
                    },
                    invalidMoves = new[]
                    {
                        Move(Verb.PresentEvidence, rh1, false, 1, false,
                            "The High Priestess's holy fork? That proves her sanctity, not my guilt! Are your wits as rusted as your plates?!"),
                    },
                },
                new InterrogationPhase
                {
                    suspectClaim = "I am a faithful servant of Saint Althea! I have dedicated twenty years to ringing these bells! What possible motive could I have to harm Mother Lyra?!",
                    keywords = new[] { "What possible motive could I have" },
                    validMoves = new[]
                    {
                        c2Press2,
                        Move(Verb.Contradict, c3, true, 0, false,
                            "You didn't want to harm her, Accord. You were cornered. The hooded stranger held your family's exile over your head."),
                        Move(Verb.Contradict, c5, true, 0, false,
                            "Your sister Marian's sanctuary debt was bought out by a veil-sealed creditor due at sunset. They used her asylum to force your hand, Accord."),
                    },
                    invalidMoves = new MoveData[0],
                },
            };
            EditorUtility.SetDirty(interrogation);

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
            caseData.interrogation = interrogation;
            caseData.clues = new[] { c1, c2, c3, c4, c5, t1, t2, rh1, l2 };
            caseData.latentLog = log;
            caseData.toolUnlocked = Tool.None;
            caseData.uneaseFlagKey = "Case2_UneaseActive";
            caseData.hasInvestigationScene = true;
            caseData.hasInterrogation = true;
            caseData.preloadedClues = new ClueData[0];
            caseData.evidenceGraph = LoadCanonicalGraph(
                "Assets/Data/Cases/Case02/EVG_02.asset");
            EditorUtility.SetDirty(caseData);
        }

        static void GenerateCase3(string folder)
        {
            EnsureFolder(folder);

            var c1 = Clue(folder, "C1_03", "Brass Extraction Siphon", ClueType.Physical, 3,
                AcquisitionMethod.Raycast3D, Tool.None,
                "<b>Industrial pneumatic tap</b> driven deep into the primary heartwood taproot. High-pressure <b>forced sap drainage</b> channeling raw environmental mana into pressurized canisters.",
                hasWorldPosition: true, worldPosition: new Vector2(0, 8));
            var c2 = Clue(folder, "C2_03", "Inscribed Silver Ritual Dagger", ClueType.Physical, 3,
                AcquisitionMethod.Raycast3D, Tool.None,
                "Ceremonial silver dagger bearing the <b>eclipsed eye insignia of the Veiled Circle</b>. The blade edge is coated with the same <b>solvent volatility stabilizer</b> identified in Case 1.",
                hasWorldPosition: true, worldPosition: new Vector2(-4, 0));
            var c3 = Clue(folder, "C3_03", "Vane's Discarded Ritual Cloak", ClueType.Physical, 3,
                AcquisitionMethod.EmotionEcho, Tool.EmotionEcho,
                "Vane's discarded ritual cloak at the altar; read with <b>Emotion Echo</b>, revealing lingering residue of <b>fanatical zeal and deceit</b> tying him to the <b>Veiled Circle's Sanctum Core ritual</b>.",
                hasWorldPosition: true, worldPosition: new Vector2(-4, 4));
            var c4 = Clue(folder, "C4_03", "Sealed Mana-Resin Canister", ClueType.Physical, 3,
                AcquisitionMethod.Raycast3D, Tool.None,
                "Pressurized brass canister filled with luminous sap, tagged with a <b>Veiled Circle shipping stencil</b> routed to <b>Iron Hollow Drift 09</b> — matching the <b>solvent-coated dagger's</b> stabilizing residue.",
                hasWorldPosition: true, worldPosition: new Vector2(4, 2));
            var c5 = Clue(folder, "C5_03", "Vane's Five-Night Harvest Tally", ClueType.Physical, 3,
                AcquisitionMethod.Inspection2D, Tool.None,
                "Waterproof field notebook left at Vane's boundary outpost logging <b>five consecutive nights of canister yields</b> matching the exact onset of the Elder Tree's collapse.");
            var t1 = Clue(folder, "T1_03", "Ranger Scout's Report", ClueType.Testimony, 3,
                AcquisitionMethod.Dialogue2D, Tool.None,
                "The rot began abruptly <b>five nights ago</b>, radiating from the altar clearing. <b>No natural disease</b> drains vital sap that fast without leaving fungal rot.",
                dissonanceKeyword: "natural forest blight");
            var t2 = Clue(folder, "T2_03", "Vane's Fanatical Justification", ClueType.Testimony, 3,
                AcquisitionMethod.Dialogue2D, Tool.None,
                "I am merely a wandering botanical researcher studying <b>natural forest blight</b>. You meddling guild hounds are disturbing a <b>sanctioned ecological survey</b>!",
                dissonanceKeyword: "sanctioned ecological survey");
            var rh1 = Clue(folder, "RH1_03", "Blighted Bark Shavings", ClueType.RedHerring, 3,
                AcquisitionMethod.Inspection2D, Tool.None,
                "Stripped bark infested with common wood-boring beetles. A <b>natural secondary infestation</b> attracted by the dying tree, unrelated to deep mana drainage.");
            var l3 = Clue(folder, "L3_03", "Runic Log #3: Displacement Device Telemetry", ClueType.Latent, 3,
                AcquisitionMethod.AetherSight, Tool.AetherSight,
                "Available only after Case 4, when Aether Sight is unlocked. Decrypted pre-cataclysm technical readout recording the catastrophic mana displacement event. Contains Elise Cochran's final transmission before the world reshaped, revealing the first tangible proof of her sacrifice.",
                isBacktrackingOnly: true, hasWorldPosition: true, worldPosition: new Vector2(0, 4));

            var pressMove = Move(Verb.Press, null, false, 0, false,
                "The regional council granted me unrestricted access to sample fungal spread! My tools are delicate biological instruments, not weapons!");
            pressMove.emotionAura = Aura.Amber;
            var press2Move = Move(Verb.Press, null, false, 0, false,
                "Alchemical houses pay fortunes for raw spirit-resin! I am an opportunist, nothing more! Smuggling contraband is a municipal civil infraction, not a hanging offense!");
            press2Move.emotionAura = Aura.Violet;
            var press3Move = Move(Verb.Press, null, false, 0, false,
                "Nature dies to make way for the strong! This woodland decay is localized, irrelevant, and forgotten by tomorrow's sunrise!");
            press3Move.emotionAura = Aura.Amber;

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
                        Move(Verb.Contradict, c5, true, 0, true,
                            "Your own field notebook logs five consecutive nights of pressurized canister yields — matching the exact onset of the grove's collapse. That is an extraction schedule, not an ecological survey."),
                    },
                    invalidMoves = new MoveData[0],
                },
                new InterrogationPhase
                {
                    suspectClaim = "Fine, I tapped the tree for alchemical commerce! But a lone merchant selling sap on the black market hardly concerns your guild's grand conspiracy theories!",
                    keywords = new[] { "lone merchant selling sap" },
                    validMoves = new[]
                    {
                        press2Move,
                        Move(Verb.PresentEvidence, c2, true, 0, false,
                            "This redirects your 'lone merchant' story. Black-market traders do not carry daggers bearing the eclipsed eye of the Veiled Circle, treated with the exact same chemical solvent used in Asterveil's vault. Who do you actually work for?"),
                        Move(Verb.PresentEvidence, c4, true, 0, false,
                            "Lone black-market merchants do not stencil pressurized mana canisters for Veiled Circle delivery to Iron Hollow Drift 09. You are a supply operative."),
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
                    keywords = new[] { "no proof this tree connects to anything" },
                    validMoves = new[]
                    {
                        press3Move,
                        Move(Verb.Contradict, c3, true, 0, false,
                            "Your own cloak betrays you, Vane \u2014 the Emotion Echo reads the fanatical zeal and deceit soaked into it. You serve the Veiled Circle's Sanctum Core ritual, and this tree feeds it."),
                    },
                    invalidMoves = new MoveData[0],
                },
            };
            EditorUtility.SetDirty(interrogation);

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
            caseData.interrogation = interrogation;
            caseData.clues = new[] { c1, c2, c3, c4, c5, t1, t2, rh1, l3 };
            caseData.latentLog = log;
            caseData.toolUnlocked = Tool.EmotionEcho;
            caseData.uneaseFlagKey = "Case3_UneaseActive";
            caseData.hasInvestigationScene = true;
            caseData.hasInterrogation = true;
            caseData.preloadedClues = new ClueData[0];
            caseData.evidenceGraph = LoadCanonicalGraph(
                "Assets/Data/Cases/Case03/EVG_03.asset");
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

        /// <summary>
        /// The weighted board is the canonical deduction path, so generated
        /// cases link the authored EvidenceGraphData from the canonical
        /// Assets/Data/Cases folder regardless of the generation root.
        /// </summary>
        static EvidenceGraphData LoadCanonicalGraph(string canonicalPath)
        {
            var graph = AssetDatabase.LoadAssetAtPath<EvidenceGraphData>(canonicalPath);
            if (graph == null)
                Debug.LogWarning($"CaseDataGenerator: canonical evidence graph not found at '{canonicalPath}'.");
            return graph;
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
