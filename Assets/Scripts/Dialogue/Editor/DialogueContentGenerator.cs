using System;
using System.Collections.Generic;
using System.IO;
using Morsoul.Data;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Dialogue.Editor
{
    /// <summary>
    /// Authors the Case 1 Lower Market District (Zone A) DialogueData assets from
    /// Narrative.md section 4.1.1. Idempotent: existing assets at the target paths
    /// are loaded and overwritten field-by-field, never duplicated. Dialogue text
    /// is copied verbatim from the doc (including U+2019 apostrophes and U+2014
    /// em dashes); the only invented lines are the Ovan re-ask hub prompt, the
    /// post-warrant closing line and the locked-gate refusal, all marked
    /// "INVENTED" in comments below.
    /// </summary>
    public static class DialogueContentGenerator
    {
        const string DialogueFolder = "Assets/Data/Dialogue/Case01";
        const string ClueFolder = "Assets/Data/Cases/Case01";

        /// <summary>Matches GateInteractable.requiredFlag on the VaultGate in HubSceneFactory.</summary>
        const string WarrantFlag = "C1_WarrantGranted";

        [MenuItem("Morsoul/Data/Generate Case 1 Hub Dialogue")]
        static void GenerateFromMenu()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Generate Case 1 Hub Dialogue",
                "This will (re)generate the Case 1 Lower Market dialogue assets under " +
                "Assets/Data/Dialogue/Case01, overwriting all field values on any " +
                "existing assets. Continue?",
                "Generate",
                "Cancel");
            if (!proceed) return;

            Generate();
            ValidateAllDialogue();
        }

        [MenuItem("Morsoul/Data/Validate Dialogue Assets")]
        public static void ValidateAllDialogue()
        {
            int errorCount = 0;
            int checkedCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:DialogueData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
                if (data == null) continue;
                checkedCount++;
                errorCount += Validate(data, path);
            }
            if (errorCount == 0)
                Debug.Log($"Validate Dialogue Assets: {checkedCount} DialogueData asset(s) passed.");
            else
                Debug.LogError($"Validate Dialogue Assets: {errorCount} error(s) across " +
                               $"{checkedCount} DialogueData asset(s).");
        }

        public static void Generate()
        {
            EnsureFolder(DialogueFolder);

            // T1_01 = Guildmaster's Key Registry, T2_01 = Ignatius' Initial Claim.
            var keyRegistry = LoadClue("T1_01");
            var ignatiusClaim = LoadClue("T2_01");

            GenerateOvan(keyRegistry);
            GenerateIgnatiusClaim(ignatiusClaim);
            GenerateMarketBarks();
            GenerateGateLocked();
            // D_Eliza_Case1_Intro_01 intentionally not generated: Narrative.md
            // section 4.1.1 authors no standalone Eliza case-briefing scene;
            // her Case 1 lines live inside the Ovan tree instead.

            AssetDatabase.SaveAssets();
            // Newly created assets are not imported until a refresh; without this,
            // LoadAssetAtPath on a first run can return the in-memory object with
            // unsaved default field values (empty refs/arrays/flags).
            AssetDatabase.Refresh();
        }

        // --- Asset authors -----------------------------------------------------

        /// <summary>
        /// Guildmaster Ovan conversation: greeting chain, then a three-option hub.
        /// Security and Ignatius branches loop back to the hub so the player can
        /// ask everything; the warrant branch grants C1_WarrantGranted and ends
        /// the conversation (no return to the menu, per the doc).
        /// </summary>
        static void GenerateOvan(ClueData keyRegistry)
        {
            Dialogue("D_Ovan_01", "ovan_has_warrant",
                // Repeat interaction after warrant is already granted:
                // If warrant not yet granted, GoTo skips to nextNodeId ("ovan_repeat_briefed").
                Node("ovan_has_warrant", "Guildmaster Ovan",
                    "(Waves a hurried hand) \"You already have the Guild storage clearance warrant, Eliza! " +
                    "The lower cellar grate in the Lower Market is yours to unseal. Check the vault before Ignatius turns the street upside down.\"",
                    requiresFlag: WarrantFlag,
                    next: "ovan_repeat_briefed"),

                // Repeat interaction after briefing seen but before warrant granted:
                // If briefing not yet seen, GoTo skips to nextNodeId ("ovan_greeting").
                Node("ovan_repeat_briefed", "Guildmaster Ovan",
                    "\"Everything from the Consortium report is on my desk. What do you still need before heading down to the Lower Market?\"",
                    requiresFlag: "C1_BriefingSeen",
                    options: new[]
                    {
                        Option("Inquire about the vault's security", "ovan_security"),
                        Option("Ask about Master Ignatius", "ovan_ignatius"),
                        Option("Request storage clearance (Forensic Entry Warrant)", "ovan_warrant"),
                    },
                    next: "ovan_greeting"),

                Node("ovan_greeting", "Eliza",
                    "All right, Ovan\u2014we're ready for the full dossier on the Lower Market vault incident.",
                    next: "ovan_challenge"),
                Node("ovan_challenge", "Guildmaster Ovan",
                    "(Rubbing his temples over a stack of wax-sealed dispatches) " +
                    "\"Good. The Merchant Consortium is demanding answers by noon, and " +
                    "this makes a clean first field contract for your new partner here.\"",
                    next: "golem_scan"),
                Node("golem_scan", "Golem",
                    "[OPTICAL SCAN: Guildmaster respiration elevated. Dispatches " +
                    "stamped 'URGENT \u2014 MUNICIPAL TREASURY'.] \"Unit ready, " +
                    "Guildmaster. State the parameters of the Lower Market vault " +
                    "breach.\"",
                    next: "ovan_briefing"),
                Node("ovan_briefing", "Guildmaster Ovan",
                    "Master Alchemist Ignatius\u2019 subterranean storage vault\u2014the one " +
                    "holding the seasonal reserve of Greater Restoration Draughts\u2014was " +
                    "breached three hours before dawn. Ten thousand gold ducats worth of " +
                    "inventory, dissolved into thin air! Ignatius claims dark cultists " +
                    "used forbidden volatile magic!",
                    setsFlag: "C1_BriefingSeen",
                    next: "ovan_options"),

                // Hub node.
                Node("ovan_options", "Guildmaster Ovan",
                    "Review the Consortium's file and tell me what else you need.",
                    options: new[]
                    {
                        Option("Inquire about the vault's security", "ovan_security"),
                        Option("Ask about Master Ignatius", "ovan_ignatius"),
                        Option("Request storage clearance (Forensic Entry Warrant)", "ovan_warrant"),
                    }),

                // Branch 1: vault security -> registers T1_01 (Guildmaster's Key
                // Registry) on Eliza's closing line, then returns to the hub.
                Node("ovan_security", "Guildmaster Ovan",
                    "According to the Consortium registry, it\u2019s a masterwork Dwarven " +
                    "cylinder\u2014four tumblers, reinforced cold-iron plating, fitted into " +
                    "bedrock. You couldn't crack it with a siege ram. Only two keys " +
                    "exist: Master Ignatius carries one on his personal brass chain, " +
                    "and the duplicate stays sealed in Consortium archives under wax. " +
                    "The archive seal was never touched!",
                    next: "golem_conclusion"),
                Node("golem_conclusion", "Golem",
                    "Conclusion: Entry through the perimeter door requires an " +
                    "authentic physical key for non-destructive ingress.",
                    next: "eliza_registry"),
                Node("eliza_registry", "Eliza",
                    "(Smirks) \"Locked from the outside without a scratch on the door... " +
                    "Sounds cozy. We\u2019ll take a copy of the key registry record for our file.\"",
                    clue: keyRegistry,
                    next: "ovan_options"),

                // Branch 2: Ignatius' status -> Eliza pushes to the crime scene,
                // then returns to the hub.
                Node("ovan_ignatius", "Guildmaster Ovan",
                    "He\u2019s barricaded in his parlor down in the Lower Market, " +
                    "practically having apoplexy and demanding emergency compensation " +
                    "from the City Commercial Treasury before the noon bell! If the " +
                    "Council doesn't reimburse him, he threatens to pull all " +
                    "apothecary trade from Asterveil.",
                    next: "eliza_hurry"),
                Node("eliza_hurry", "Eliza",
                    "He\u2019s quick to demand municipal gold for a crime that barely " +
                    "cooled down. {PlayerName}, once we grab the warrant, we need to " +
                    "inspect that vault before his apprentices sweep away the truth.",
                    next: "ovan_options"),

                // Branch 3: warrant demand -> grants C1_WarrantGranted (unlocks the
                // VaultGate cellar grate) and ends the conversation.
                Node("ovan_warrant", "Guildmaster Ovan",
                    "(Sighs, stamping a Guild parchment with his heavy bronze seal) " +
                    "\"Take the Forensic Entry Warrant. Bailiff at the cellar grate " +
                    "will let you down into the Lower Market vault\u2014but watch your step, " +
                    "the whole alley still reeks of strange smoke.\"",
                    setsFlag: WarrantFlag,
                    next: "warrant_close"),
                Node("warrant_close", "Eliza",
                    "Warrant stamped and sealed. Head downstairs when you're ready, " +
                    "{PlayerName}\u2014let's see what Ignatius' vault is really hiding."));
        }

        /// <summary>
        /// Master Alchemist Ignatius dialogue graph: haughty opening monologue,
        /// formal claim registering T2_01, inquiry menu, and conditional access
        /// responses based on C1_WarrantGranted. Subsequent interactions branch
        /// directly into the menu via Ignatius_ClaimHeard.
        /// </summary>
        static void GenerateIgnatiusClaim(ClueData claim)
        {
            Dialogue("D_Ignatius_Claim_01", "ignatius_repeat",
                // Repeat interaction: if claim already registered, goes straight to options.
                // If not yet registered, GoTo skips to nextNodeId ("ignatius_greeting").
                Node("ignatius_repeat", "Master Alchemist Ignatius",
                    "You again? Stop hovering around my parlor! Unless the City Council " +
                    "has approved my emergency compensation, I have nothing to say to you.",
                    requiresFlag: "Ignatius_ClaimHeard",
                    options: new[]
                    {
                        Option("Inquire about the destroyed vault & dark magic", "ignatius_vault"),
                        Option("Inquire about crime scene access", "ignatius_access"),
                        Option("Conclude the inquiry", "ignatius_leave"),
                    },
                    next: "ignatius_greeting"),

                // Initial greeting: haughty aristocratic monologue per Specs §2.2.
                Node("ignatius_greeting", "Master Alchemist Ignatius",
                    "You dare cross my threshold with mud on your treads, automaton? " +
                    "An entire quarter-century of alchemical mastery reduced to vapor by dark magic, " +
                    "and the Guild insults me with a mechanical scrap-collector?!",
                    next: "ignatius_claim"),

                // Formal claim: registers T2_01 (Ignatius' Initial Claim).
                Node("ignatius_claim", "Master Alchemist Ignatius",
                    "A rogue infiltrator breached my inner sanctum using volatile dark " +
                    "magic, destroying my entire life\u2019s work! Ten thousand gold ducats " +
                    "worth of inventory, dissolved into thin air! If the City Commercial " +
                    "Treasury does not reimburse me before noon, Asterveil faces ruin!",
                    clue: claim,
                    setsFlag: "Ignatius_ClaimHeard",
                    next: "ignatius_menu"),

                // Central inquiry hub.
                Node("ignatius_menu", "Master Alchemist Ignatius",
                    "Well? What else does the Investigator's Guild want with me?",
                    options: new[]
                    {
                        Option("Inquire about the destroyed vault & dark magic", "ignatius_vault"),
                        Option("Inquire about crime scene access", "ignatius_access"),
                        Option("Conclude the inquiry", "ignatius_leave"),
                    }),

                // Branch 1: Vault & dark magic details.
                Node("ignatius_vault", "Master Alchemist Ignatius",
                    "Two hundred sealed crates of Batch #408 Greater Restoration Draughts! " +
                    "What else could melt through Dwarven cold-iron tumblers without tripping " +
                    "the municipal ward-chimes? Forbidden volatile sorcery! Do not lecture me " +
                    "on alchemy, construct\u2014go apprehend the dark cultists!",
                    next: "ignatius_menu"),

                // Branch 2: Access inquiry.
                Node("ignatius_access", "Master Alchemist Ignatius",
                    "The subterranean vault is sealed under strict quarantine! If you don't " +
                    "have Guildmaster Ovan's signed forensic warrant, you will not set one " +
                    "iron toe down those stairs. And if you DO have his parchment... mind your " +
                    "clunky treads down there! If you shatter a single undamaged alembic, I " +
                    "shall hold the Investigator's Guild personally liable for the damages!",
                    next: "ignatius_menu"),

                // Exit branch.
                Node("ignatius_leave", "Master Alchemist Ignatius",
                    "Hmph. Then go find the culprits and stop obstructing honest commerce!"));
        }

        /// <summary>
        /// Ambient market barks as a linear chain for the proximity-trigger
        /// use-case; speaker labels per the doc.
        /// </summary>
        static void GenerateMarketBarks()
        {
            Dialogue("D_MarketBarks_01", "bark_1",
                Node("bark_1", "Panicked Vendor",
                    "If dark sorcery can melt through Master Ignatius' Dwarven vault, " +
                    "what's stopping them from burning down the entire bazaar?! The " +
                    "Guild Council does nothing!",
                    next: "bark_2"),
                Node("bark_2", "Apothecary Apprentice",
                    "The purple smoke stayed in the alley for an hour. It stung my " +
                    "eyes, but... didn't smell like sulfur. Smelled like vinegar and " +
                    "lamp oil.",
                    next: "bark_3"),
                Node("bark_3", "City Bailiff",
                    "Move along! The Alchemist Guildhall is sealed by order of " +
                    "Magistrate Corvo. No trade until the contamination team clears " +
                    "the street."));
        }

        /// <summary>
        /// Refusal line for the locked VaultGate (GateInteractable.lockedDialogue).
        /// The doc has no spoken gate-refusal quote; the line is INVENTED in the
        /// City Bailiff's voice, consistent with his bark_3 authority.
        /// </summary>
        static void GenerateGateLocked()
        {
            Dialogue("D_Gate_Locked_01", "gate_locked",
                Node("gate_locked", "City Bailiff",
                    "Hold it! The lower cellar grate stays sealed by order of " +
                    "Magistrate Corvo. No entry without a Guild forensic warrant."));
        }

        // --- Validation --------------------------------------------------------

        static int Validate(DialogueData data, string path)
        {
            int errors = 0;
            void Report(string message)
            {
                errors++;
                Debug.LogError($"{path}: {message}", data);
            }

            // Index node ids; empty and duplicate ids break the runner's lookups.
            var byId = new Dictionary<string, DialogueNodeData>(StringComparer.Ordinal);
            var nodes = data.nodes;
            if (nodes != null)
            {
                for (int i = 0; i < nodes.Length; i++)
                {
                    var node = nodes[i];
                    if (node == null)
                    {
                        Report($"nodes[{i}] is null");
                        continue;
                    }
                    if (string.IsNullOrEmpty(node.id))
                    {
                        Report($"nodes[{i}] has an empty id");
                        continue;
                    }
                    if (!byId.TryAdd(node.id, node))
                        Report($"duplicate node id '{node.id}'");
                }
            }
            if (byId.Count == 0)
                Report("dialogue has no usable nodes");

            // Start node must resolve.
            if (string.IsNullOrEmpty(data.startNodeId))
                Report("startNodeId is empty");
            else if (!byId.ContainsKey(data.startNodeId))
                Report($"startNodeId '{data.startNodeId}' matches no node id");

            // Edge table + dangling-link check. An empty nextNodeId (node or
            // option) is a legal dialogue end and is not flagged.
            var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var pair in byId)
                edges[pair.Key] = new List<string>();
            foreach (var node in byId.Values)
            {
                var outEdges = edges[node.id];
                if (!string.IsNullOrEmpty(node.nextNodeId))
                {
                    if (!byId.ContainsKey(node.nextNodeId))
                        Report($"node '{node.id}' nextNodeId '{node.nextNodeId}' " +
                               "matches no node id");
                    else
                        outEdges.Add(node.nextNodeId);
                }
                if (node.options == null) continue;
                for (int i = 0; i < node.options.Length; i++)
                {
                    var option = node.options[i];
                    if (option == null)
                    {
                        Report($"node '{node.id}' options[{i}] is null");
                        continue;
                    }
                    if (string.IsNullOrEmpty(option.nextNodeId)) continue;
                    if (!byId.ContainsKey(option.nextNodeId))
                        Report($"node '{node.id}' option '{option.label}' nextNodeId " +
                               $"'{option.nextNodeId}' matches no node id");
                    else
                        outEdges.Add(option.nextNodeId);
                }
            }

            // registersClue must be a live ClueData reference (catches deleted
            // clue assets, which a plain null check misses via Unity fake-null).
            var serialized = new SerializedObject(data);
            var nodesProp = serialized.FindProperty("nodes");
            if (nodesProp != null)
            {
                for (int i = 0; i < nodesProp.arraySize; i++)
                {
                    var nodeProp = nodesProp.GetArrayElementAtIndex(i);
                    var clueProp = nodeProp.FindPropertyRelative("registersClue");
                    if (clueProp == null) continue;
                    var idProp = nodeProp.FindPropertyRelative("id");
                    string id = idProp != null ? idProp.stringValue : $"nodes[{i}]";
                    if (clueProp.objectReferenceEntityIdValue != EntityId.None &&
                        clueProp.objectReferenceValue == null)
                    {
                        Report($"node '{id}' registersClue is a missing ClueData " +
                               "reference (deleted asset?)");
                    }
                    else if (clueProp.objectReferenceValue != null &&
                             !(clueProp.objectReferenceValue is ClueData))
                    {
                        Report($"node '{id}' registersClue is not a ClueData " +
                               $"({clueProp.objectReferenceValue.GetType().Name})");
                    }
                }
            }

            // Every node must be reachable from the start.
            if (!string.IsNullOrEmpty(data.startNodeId) &&
                byId.ContainsKey(data.startNodeId))
            {
                var seen = Reachable(edges, data.startNodeId);
                foreach (var id in byId.Keys)
                    if (!seen.Contains(id))
                        Report($"node '{id}' is unreachable from startNodeId " +
                               $"'{data.startNodeId}'");
            }

            // Cycles are legal only as loops back to a hub node with an option
            // exit - report them as warnings, not errors.
            var cyclic = new List<string>();
            foreach (var id in byId.Keys)
                if (Reachable(edges, id).Contains(id))
                    cyclic.Add(id);
            if (cyclic.Count > 0)
                Debug.LogWarning($"{path}: {cyclic.Count} node(s) sit on a loop: " +
                                 string.Join(", ", cyclic) + ". Legal only when the " +
                                 "loop returns to a hub node with an option exit.", data);

            return errors;
        }

        /// <summary>All node ids reachable from start following nextNodeId + option edges.</summary>
        static HashSet<string> Reachable(Dictionary<string, List<string>> edges, string start)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (!seen.Add(current)) continue;
                if (!edges.TryGetValue(current, out var nexts)) continue;
                foreach (string next in nexts)
                    queue.Enqueue(next);
            }
            return seen;
        }

        // --- Helpers ------------------------------------------------------------

        static ClueData LoadClue(string clueId)
        {
            string path = $"{ClueFolder}/{clueId}.asset";
            var clue = AssetDatabase.LoadAssetAtPath<ClueData>(path);
            if (clue == null)
                Debug.LogWarning($"DialogueContentGenerator: clue '{clueId}' not found at " +
                                 $"'{path}'. Run 'Morsoul/Data/Generate Case 0 + 1 Assets' " +
                                 "first; registersClue left empty.");
            return clue;
        }

        static DialogueData Dialogue(string id, string startNodeId,
            params DialogueNodeData[] nodes)
        {
            var data = LoadOrCreate<DialogueData>($"{DialogueFolder}/{id}.asset");
            data.id = id;
            data.nodes = nodes;
            data.startNodeId = startNodeId;
            EditorUtility.SetDirty(data);
            return data;
        }

        static DialogueNodeData Node(string id, string speaker, string text,
            string next = null, string setsFlag = null, string requiresFlag = null,
            ClueData clue = null, DialogueOptionData[] options = null)
        {
            return new DialogueNodeData
            {
                id = id,
                speakerName = speaker,
                text = text,
                registersClue = clue,
                setsFlag = setsFlag ?? "",
                requiresFlag = requiresFlag ?? "",
                options = options,
                nextNodeId = next ?? "",
            };
        }

        static DialogueOptionData Option(string label, string next, string requiresFlag = null)
        {
            return new DialogueOptionData
            {
                label = label,
                nextNodeId = next,
                requiresFlag = requiresFlag ?? "",
            };
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
    }
}
