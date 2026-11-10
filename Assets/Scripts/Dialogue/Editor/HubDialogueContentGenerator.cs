using System.Collections.Generic;
using System.IO;
using Morsoul.Data;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Dialogue.Editor
{
    /// <summary>
    /// Authors the Zone A hub DialogueData assets for Cases 2–4 plus the
    /// post-Case-4 backtrack interludes, from Narrative.md sections
    /// 4.2.1/4.3.1/4.4.1 and I.1–I.3. Idempotent like
    /// <see cref="DialogueContentGenerator"/>: existing assets are loaded and
    /// overwritten field-by-field. Dialogue text is copied verbatim
    /// (including U+2019 apostrophes, U+2014 em dashes and the gold
    /// &lt;color=#E5A93C&gt; dissonance keyword markup); invented
    /// connective/refusal lines are marked "INVENTED" in comments.
    /// </summary>
    public static class HubDialogueContentGenerator
    {
        const string Case02Folder = "Assets/Data/Dialogue/Case02";
        const string Case03Folder = "Assets/Data/Dialogue/Case03";
        const string Case04Folder = "Assets/Data/Dialogue/Case04";
        const string InterludesFolder = "Assets/Data/Dialogue/Interludes";

        /// <summary>Matches GateInteractable.requiredFlag on the belfry stair gate.</summary>
        public const string BelfryKeyFlag = "C2_BelfryKeyGranted";
        /// <summary>Matches GateInteractable.requiredFlag on the briar barrier.</summary>
        public const string PerimeterSealFlag = "C3_PerimeterSealGranted";
        /// <summary>Matches GateInteractable.requiredFlag on the deep-shaft lift.</summary>
        public const string OverrideClearanceFlag = "C4_OverrideClearance";

        [MenuItem("Morsoul/Data/Generate Hub Dialogue (Cases 2-4 + Interludes)")]
        static void GenerateFromMenu()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Generate Hub Dialogue (Cases 2-4 + Interludes)",
                "This will (re)generate hub dialogue assets under " +
                "Assets/Data/Dialogue/{Case02,Case03,Case04,Interludes}, overwriting all " +
                "field values on existing assets. Continue?",
                "Generate",
                "Cancel");
            if (!proceed) return;

            Generate();
            DialogueContentGenerator.ValidateAllDialogue();
        }

        public static void Generate()
        {
            EnsureFolder(Case02Folder);
            EnsureFolder(Case03Folder);
            EnsureFolder(Case04Folder);
            EnsureFolder(InterludesFolder);

            GenerateCase02();
            GenerateCase03();
            GenerateCase04();
            GenerateInterludes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // --- Case 2: Cathedral Nave & Cloister (Narrative §4.2.1) ----------------

        static void GenerateCase02()
        {
            var plea = LoadClue("Case02", "T1_02");
            var accusation = LoadClue("Case02", "T2_02");

            // Ambient Cathedral Barks (proximity triggers).
            Dialogue(Case02Folder, "D_NaveBarks_02", "bark_1",
                Node("bark_1", "Devout Matron",
                    "Mother Lyra was mid-verse in the Benediction when the bells struck " +
                    "six! The note rang sour, like cracked lead, and her voice was torn " +
                    "right from her throat! Only demonic witchcraft steals a holy hymn!",
                    next: "bark_2"),
                Node("bark_2", "Zealous Templar Guard",
                    "It\u2019s those pagan wanderers from the outer woods. We allowed their " +
                    "acolyte to sweep the cloisters out of mercy, and they repay the Church " +
                    "with a silencing curse. The gallows are too kind.",
                    next: "bark_3"),
                Node("bark_3", "Cathedral Novice",
                    "I saw Accord shivering in the choir pews after the bells stopped. " +
                    "He\u2019s rung that tower for thirty years; he looked like he saw a " +
                    "specter up in the rafters."));

            // Scene 1: Valerius & Lyra at the high altar (no clue).
            Dialogue(Case02Folder, "D_Valerius_Lyra_02", "valerius_halt",
                Node("valerius_halt", "Templar Captain Valerius",
                    "(Barring his halberd across the nave stairs) \"Halt. The Sanctuary " +
                    "is under martial quarantine. By decree of the Orthodoxy, no one " +
                    "ascends the altar steps while Mother Lyra is under spiritual " +
                    "affliction.\"",
                    next: "eliza_warrant"),
                Node("eliza_warrant", "Eliza",
                    "(Flashes the bronze Investigator's Guild badge) \"Lower the polearm, " +
                    "Captain. Magistrate Corvo signed our jurisdictional warrant. An " +
                    "attack on an ecumenical leader inside Asterveil falls squarely " +
                    "under the Investigator's Guild.\"",
                    next: "valerius_scorn"),
                Node("valerius_scorn", "Templar Captain Valerius",
                    "(Scowls, looking down at the Golem's stone frame) \"An automaton " +
                    "and a street rogue? The culprit is already in irons, Astra. The " +
                    "Spirit-folk acolyte cast a foul hex from the nave floor during " +
                    "vespers. We don't need a walking relic meddling in holy mysteries.\"",
                    next: "golem_scan"),
                Node("golem_scan", "Golem",
                    "[OPTICAL SCAN: Subject 01 (Valerius) adrenaline standard, " +
                    "ideological confirmation bias high.]\n" +
                    "[OPTICAL SCAN: Subject 02 (High Priestess Lyra) vocal chords coated " +
                    "in harmonic crystalline residue. Respiration stable. Zero demonic " +
                    "corruption detected.]\n" +
                    "\"Observation: High Priestess Lyra exhibits acoustic mana " +
                    "suppression, not biological necrosis. Permission requested to take " +
                    "her deposition.\"",
                    next: "lyra_slate"),
                Node("lyra_slate", "High Priestess Lyra",
                    "(Approaches gently, dressed in white ceremonial robes. She places a " +
                    "hand on Valerius' arm, gesturing with soft authority to lower his " +
                    "halberd. She holds a small slate tablet and writes quickly with " +
                    "chalk, showing it to the Golem:)\n" +
                    "\"Peace, Captain. Truth does not fear scrutiny. Welcome, seekers " +
                    "of clarity. My voice is trapped beneath heavy glass, but my mind " +
                    "is whole. Speak with the child Sylvan before judgment falls.\"",
                    next: "eliza_thanks"),
                Node("eliza_thanks", "Eliza",
                    "\"Thank you, Mother Lyra. We'll find out what really poisoned " +
                    "the air.\""));

            // Scene 2: Sylvan interview -> registers T1_02.
            Dialogue(Case02Folder, "D_Sylvan_02", "sylvan_plea",
                Node("sylvan_plea", "Acolyte Sylvan",
                    "(Timid Spirit-folk youth with pointed ears and iridescent hair; " +
                    "shivering in cold-iron manacles) \"Please... I swear by the river " +
                    "spirits, I didn't cast anything! I was only refilling the " +
                    "sanctuary oil lamps!\"",
                    next: "eliza_easy"),
                Node("eliza_easy", "Eliza",
                    "\"Easy, kid. We're not Templars. Take a breath and walk us " +
                    "through the sixth bell.\"",
                    next: "sylvan_bell"),
                Node("sylvan_bell", "Acolyte Sylvan",
                    "\"The Great Bell began to toll for evening prayer. I was standing " +
                    "near the west colonnade, pouring lantern oil right in front of " +
                    "Brother Kenneth. When the third chime rang out, there was this... " +
                    "this screeching vibration in my teeth. Like glass breaking inside " +
                    "my skull. Then Mother Lyra dropped her prayer book and clutched " +
                    "her throat.\"",
                    next: "golem_inquiry"),
                Node("golem_inquiry", "Golem",
                    "\"Did you project any spiritual mana during the hymn?\"",
                    next: "sylvan_never"),
                Node("sylvan_never", "Acolyte Sylvan",
                    "\"Never! If our kind channel magic near consecrated marble, the " +
                    "stone burns our skin! Brother Kenneth was watching my hands the " +
                    "entire time. But the second Mother Lyra collapsed, Accord came " +
                    "running down from the spiral stair, screaming that my " +
                    "<color=#E5A93C>wild spirit magic cursed the sanctuary from " +
                    "the floor!</color>\"",
                    clue: plea,
                    next: "eliza_convenient"),
                Node("eliza_convenient", "Eliza",
                    "\"Convenient for Accord. He had a suspect lined up before the " +
                    "bell even stopped swinging.\""));

            // Scene 3: Accord at the tower base -> registers T2_02, grants key.
            Dialogue(Case02Folder, "D_Accord_02", "accord_prayer",
                Node("accord_prayer", "Accord",
                    "(An elderly man with trembling hands, clutching a silver Saint " +
                    "Althea pendant tightly against his woolen vest) \"Merciful " +
                    "Saint... preserve her... wash the evil from our bells...\"",
                    next: "eliza_accord"),
                Node("eliza_accord", "Eliza",
                    "\"Accord. You were up in the tower when the chime broke.\"",
                    next: "accord_startled"),
                Node("accord_startled", "Accord",
                    "(Jumps, startled) \"Astra! Don't sneak up on an old man! Yes, " +
                    "I was at the ropes! Thirty years I\u2019ve rung the vespers, and " +
                    "never has the Great Bell groaned like that! That heathen boy... " +
                    "I saw him through the floor grate! He <color=#E5A93C>chanted " +
                    "from the tower base and bewitched the machinery! He must " +
                    "have!</color>\"",
                    clue: accusation,
                    next: "golem_acoustic"),
                Node("golem_acoustic", "Golem",
                    "[ACOUSTIC SIMULATION: Sound dampening across vertical shaft]\n" +
                    "\"Inquiry: The belfry ceiling sits thirty-two meters above the " +
                    "nave floor. Direct vocal enchantment through solid granite " +
                    "arches would require twenty times the legal mana threshold. Did " +
                    "you observe any physical tampering with the bell itself?\"",
                    next: "accord_denial"),
                Node("accord_denial", "Accord",
                    "(Sweating profusely, eyes darting toward the rafter door) " +
                    "\"Tampering?! No! Absolutely not! No one touches Saint Althea\u2019s " +
                    "Bell but me! I keep the tower door locked with my own brass ring! " +
                    "The curse came from the ground, I tell you!\"",
                    next: "eliza_key"),
                Node("eliza_key", "Eliza",
                    "\"If nobody else touched it, then you won't mind if we take a " +
                    "look at the bell. Hand over the tower key, Accord.\"",
                    next: "accord_key"),
                Node("accord_key", "Accord",
                    "(Hesitates, trembling, then fumbles a heavy iron key off his " +
                    "belt) \"Take it! But beware the rafters... the devil\u2019s soot " +
                    "is still thick up there.\"",
                    setsFlag: BelfryKeyFlag));

            // INVENTED locked-gate refusal (doc has no spoken line) in a
            // Templar guard's voice, consistent with the quarantine barks.
            Dialogue(Case02Folder, "D_Gate_Locked_02", "gate_locked",
                Node("gate_locked", "Templar Guard",
                    "The belfry stair stays sealed under martial quarantine. No one " +
                    "climbs that tower without the bell-ringer's own key."));
        }

        // --- Case 3: Whisperwood Boundary Trail (Narrative §4.3.1) --------------

        static void GenerateCase03()
        {
            var report = LoadClue("Case03", "T1_03");
            var defense = LoadClue("Case03", "T2_03");

            Dialogue(Case03Folder, "D_ForestBarks_03", "bark_1",
                Node("bark_1", "Forest Ranger",
                    "The wild spirits aren't singing anymore\u2014they\u2019re wailing. " +
                    "Three woodcutters were mauled at the perimeter yesterday because " +
                    "the tree-guardians have gone completely feral with pain.",
                    next: "bark_2"),
                Node("bark_2", "Desperate Dryad Acolyte",
                    "Look at the soil... It\u2019s turning into dry gray ash. The " +
                    "Elder Tree has anchored this grove for ten thousand years. A " +
                    "tree like that doesn't just starve in a week!",
                    next: "bark_3"),
                Node("bark_3", "Guild Surveyor Courier",
                    "They\u2019re saying the sap is draining straight through the " +
                    "bedrock. If the Great Root dies, the sinkhole will swallow the " +
                    "eastern highway whole."));

            // Scene 1: Kaelen briefing -> registers T1_03.
            Dialogue(Case03Folder, "D_Kaelen_03", "kaelen_bow",
                Node("kaelen_bow", "Ranger Scout Kaelen",
                    "(Notching an arrow before lowering his longbow at Eliza's badge) " +
                    "\"Lady Astra! Thank the stars the Guild actually sent someone. " +
                    "Keep your construct on the hard stones\u2014the root floor ahead " +
                    "has gone soft as wet flour.\"",
                    next: "eliza_rundown"),
                Node("eliza_rundown", "Eliza",
                    "\"Give us the rundown, Kaelen. Your dispatch sounded like the " +
                    "sky was falling.\"",
                    next: "kaelen_briefing"),
                Node("kaelen_briefing", "Ranger Scout Kaelen",
                    "\"It might as well be! Five nights ago, the canopy was glowing " +
                    "silver. Then, almost overnight, the needles turned black and the " +
                    "roots began weeping resin. The Elder Spirit Tree is dying, " +
                    "Astra. And the woodland spirits have lost their minds\u2014" +
                    "screaming, attacking anyone who steps into the hollow.\"",
                    next: "golem_scan"),
                Node("golem_scan", "Golem",
                    "[SPECTRAL SCAN: Ambient atmospheric mana density down 64% from " +
                    "provincial baseline.]\n" +
                    "\"Inquiry: Has this forest ever experienced sudden botanical " +
                    "blight or parasites of this magnitude?\"",
                    next: "kaelen_never"),
                Node("kaelen_never", "Ranger Scout Kaelen",
                    "\"Never! That\u2019s what I told the scholar over in that fancy " +
                    "tent. He keeps trying to convince me this is a " +
                    "<color=#E5A93C>natural forest blight caused by root-boring " +
                    "beetles.</color> But beetles don't suck a million gallons of " +
                    "ancient sap out of a trunk in five days!\"",
                    clue: report,
                    next: "eliza_scholar"),
                Node("eliza_scholar", "Eliza",
                    "\"Scholar? Who authorized an academic camp in a hazardous " +
                    "spirit zone?\"",
                    next: "kaelen_vane"),
                Node("kaelen_vane", "Ranger Scout Kaelen",
                    "\"Fellow named Vane. Showed up with stamped transit permits " +
                    "from the capital right as the tree started drooping. Claims " +
                    "he\u2019s studying the decay. He\u2019s set up right at the " +
                    "clearing entrance.\""));

            // Scene 2: Vane confrontation -> registers T2_03, grants seal.
            Dialogue(Case03Folder, "D_Vane_03", "vane_greeting",
                Node("vane_greeting", "Vane",
                    "(Polite, clean-shaven man in high-collared scholarly robes, " +
                    "calmly examining a specimen vial of murky gray fluid) " +
                    "\"Greetings, travelers. If you are seeking passage into the " +
                    "deeper grove, I strongly advise turning back. The fungal " +
                    "contamination within the hollow has agitated the local dryads " +
                    "beyond rational diplomacy.\"",
                    next: "eliza_guild"),
                Node("eliza_guild", "Eliza",
                    "(Eyes narrowed, resting a hand on her daggers) \"We aren't " +
                    "travelers. Investigator's Guild. And you seem awfully " +
                    "composed for a man sitting fifty paces away from an " +
                    "ecological disaster.\"",
                    next: "vane_scholar"),
                Node("vane_scholar", "Vane",
                    "(Chuckles smoothly, adjusting iron-rimmed spectacles) \"A man " +
                    "of science cannot afford hysterics, Lady Investigator. I am " +
                    "Master Vane, accredited fellow of the Provincial Botanical " +
                    "Directorate. I am conducting a <color=#E5A93C>sanctioned " +
                    "ecological survey to isolate this aggressive botanical rot " +
                    "before it spreads to agricultural timber.</color>\"",
                    clue: defense,
                    next: "golem_scan"),
                Node("golem_scan", "Golem",
                    "[OPTICAL SCAN: Subject heart rate: 58 BPM (Abnormally calm). " +
                    "No biological field sampling reagents present on workbench.]\n" +
                    "\"Observation: Your laboratory lacks sterile incubation baths, " +
                    "fungal culture mediums, or spore isolation glassware. Your " +
                    "equipment consists entirely of high-pressure fluid valves and " +
                    "empty heavy-duty iron transport drums.\"",
                    next: "vane_hardens"),
                Node("vane_hardens", "Vane",
                    "(Expression hardens for an instant before smoothing into a " +
                    "patronizing smile) \"Heavy-duty sampling requires heavy-duty " +
                    "storage, automaton. Ancient trees do not yield delicate sap. " +
                    "Now, if you will excuse me, my research permits grant me " +
                    "exclusive surveying rights inside the inner sanctum.\"",
                    next: "eliza_warrant"),
                Node("eliza_warrant", "Eliza",
                    "\"Your permit doesn't supersede a Class-A Guild Warrant. " +
                    "Step aside, 'Scholar.' {PlayerName}, scan the gate.\"",
                    setsFlag: PerimeterSealFlag));

            // INVENTED locked-gate refusal (doc has no spoken line), Kaelen.
            Dialogue(Case03Folder, "D_Gate_Locked_03", "gate_locked",
                Node("gate_locked", "Ranger Scout Kaelen",
                    "Hold on\u2014the briar barrier is still sealed. Nobody passes " +
                    "the heartwood threshold without the Elder Grove Perimeter Seal."));
        }

        // --- Case 4: Iron Hollow Railhead (Narrative §4.4.1) --------------------

        static void GenerateCase04()
        {
            var workLog = LoadClue("Case04", "T1_04");
            var contractorDefense = LoadClue("Case04", "T2_04");

            Dialogue(Case04Folder, "D_IndustrialBarks_04", "bark_1",
                Node("bark_1", "Exhausted Drill Hand",
                    "The pressure gauges didn't creep up\u2014they spiked like " +
                    "lightning! Chief Brokk was in that lower chamber inspecting the " +
                    "rock seam when the pneumatic main ruptured. That wasn't a slow " +
                    "leak; that was a clean blow!",
                    next: "bark_2"),
                Node("bark_2", "Union Steward",
                    "Corporate says it's operator error. They always blame the man " +
                    "holding the wrench. If Brokk dies in that stasis vault, the " +
                    "whole shift walks out!",
                    next: "bark_3"),
                Node("bark_3", "Railhead Loader",
                    "Those unmarked lead-lined bladders from Whisperwood... they " +
                    "didn't unload them at the refinery. They hauled them straight " +
                    "down past the quarantine gate into the deep fissure line under " +
                    "special contractor escort."));

            // Scene 1: Thrun gantry scene -> T1_04 on the report line, T2_04 on
            // Harlan's interjected defense, clearance flag on the key handover.
            Dialogue(Case04Folder, "D_Thrun_04", "thrun_wrench",
                Node("thrun_wrench", "Overseer Thrun",
                    "(Slamming a heavy brass wrench against the railing) \"I don't " +
                    "care about quarterly tonnage quotas, you desk-sitting parasites! " +
                    "My Chief Engineer is suffocating behind half a mile of " +
                    "collapsed granite!\"",
                    next: "eliza_sigil"),
                Node("eliza_sigil", "Eliza",
                    "(Steps onto the gantry, flashing the Investigator's Guild " +
                    "bronze sigil) \"Then save your breath for someone who can " +
                    "clear the rubble, Thrun. Investigator's Guild. What happened " +
                    "down in Drift 09?\"",
                    next: "thrun_disaster"),
                Node("thrun_disaster", "Overseer Thrun",
                    "\"A total disaster! An hour before shift change, the main " +
                    "pneumatic steam bypass blew its housing. Three hundred tons " +
                    "of bedrock collapsed into the lower junction, sealing off the " +
                    "emergency stasis chamber. Chief Engineer Brokk sealed himself " +
                    "inside to keep the toxic sulfur gas from venting into the " +
                    "residential quarters.\"",
                    next: "golem_scan"),
                Node("golem_scan", "Golem",
                    "[OPTICAL SCAN: Ambient sulfur dioxide concentration: 140 PPM " +
                    "(Elevated). Structural bedrock: Fractured by concentrated " +
                    "explosive pressure, not natural tectonic faulting.]\n" +
                    "\"Inquiry: What caused the initial pneumatic breach?\"",
                    next: "harlan_smirk"),
                Node("harlan_smirk", "Harlan",
                    "(Steps out from the shadows of the tool shed, wiping soot from " +
                    "his leather vest with an indifferent smirk) \"Simple as a snapped pick, " +
                    "automaton. Old Brokk was pushing the drilling rigs past " +
                    "maximum tolerance to hit his safety bonuses. He ignored the " +
                    "safety valves and triggered a steam surge. It was " +
                    "<color=#E5A93C>reckless operator error. Open and shut.</color>\"",
                    clue: contractorDefense,
                    next: "eliza_contractor"),
                Node("eliza_contractor", "Eliza",
                    "(Arms crossed, eyes narrowing) \"You're pretty quick to pin " +
                    "a fatal accident on a man locked in an airtight vault, " +
                    "Contractor. Who are you?\"",
                    next: "harlan_intro"),
                Node("harlan_intro", "Harlan",
                    "\"Harlan. Senior blasting contractor for the Outer Seam " +
                    "Syndicate. I oversee the tunnel powder. The Union can cry " +
                    "all they want, but the machinery failed because flesh makes " +
                    "mistakes.\"",
                    next: "thrun_defense"),
                Node("thrun_defense", "Overseer Thrun",
                    "\"Brokk has maintained those pneumatic lines for forty " +
                    "years! He doesn't make mistakes with steam pressure! Astra, " +
                    "take this bypass key. If you can get through the ventilation " +
                    "chimney into the sub-drift, find out what happened to my " +
                    "friend!\"",
                    clue: workLog,
                    setsFlag: OverrideClearanceFlag));

            // Harlan standing alone afterward: single cold line (INVENTED
            // prompt framing; the line itself is his scene dialogue reused).
            Dialogue(Case04Folder, "D_Harlan_04", "harlan_brush",
                Node("harlan_brush", "Harlan",
                    "\"The Union can cry all they want, but the machinery failed " +
                    "because flesh makes mistakes.\""));

            // INVENTED locked-gate refusal (doc has no spoken line), Thrun.
            Dialogue(Case04Folder, "D_Gate_Locked_04", "gate_locked",
                Node("gate_locked", "Overseer Thrun",
                    "The ventilation incline is sealed until I hand over the " +
                    "pneumatic override clearance\u2014and I'm not handing it to " +
                    "anyone who can't prove Guild authority."));
        }

        // --- Post-Case-4 backtrack interludes (Narrative I.1-I.3) ---------------

        static void GenerateInterludes()
        {
            // I.1 Guild Quarter — "The Guildmaster's Audit".
            Dialogue(InterludesFolder, "D_Interlude_Market_01", "ovan_greeting",
                Node("ovan_greeting", "Guildmaster Ovan",
                    "\"Ah\u2014the Guild's own miracle worker. I had new audit " +
                    "protocols bound this morning, {PlayerName}. No vault ledger " +
                    "moves a single crate without two signatures ever again.\"",
                    next: "ovan_fraud"),
                Node("ovan_fraud", "Guildmaster Ovan",
                    "\"Ignatius' fraud cost us more than gold, but we'll survive " +
                    "it. Honesty is a better collateral than pride.\"",
                    next: "eliza_solvent"),
                Node("eliza_solvent", "Eliza",
                    "\"Good. Because his solvent didn't come from a market stall. " +
                    "Whoever brokered that deal knew exactly what they were " +
                    "feeding him.\"",
                    next: "ovan_broker"),
                Node("ovan_broker", "Guildmaster Ovan",
                    "(lowering his voice) \"The broker, yes. My people traced the " +
                    "name to a dead counting house on the wharf. Empty rooms. He " +
                    "vanished before the week was out.\"",
                    next: "eliza_circle"),
                Node("eliza_circle", "Eliza",
                    "\"That's how these people work. The trail always runs cold " +
                    "the same way.\"",
                    next: "ovan_vault"),
                Node("ovan_vault", "Guildmaster Ovan",
                    "\"Well\u2014if there's anything left to learn, it's down " +
                    "in that vault of his. We keep it sealed, but you two have " +
                    "standing access.\"",
                    next: "eliza_itch"),
                Node("eliza_itch", "Eliza",
                    "(glancing toward the Iron Cellar Grate) \"Then let's take " +
                    "one more look, partner. Something down there still itches " +
                    "at the back of my mind.\""));

            // I.2 Cathedral Nave — "The Bell Still Hums".
            Dialogue(InterludesFolder, "D_Interlude_Cathedral_02", "sylvan_welcome",
                Node("sylvan_welcome", "Acolyte Sylvan",
                    "\"The cloister holds prayers for you both every evening. " +
                    "I am... welcome here again. Because of you.\"",
                    next: "lyra_thanks"),
                Node("lyra_thanks", "High Priestess Lyra",
                    "(a breath) \"You. The machine. And the light-stepper. " +
                    "Thank you.\"",
                    next: "eliza_voice"),
                Node("eliza_voice", "Eliza",
                    "\"Save the voice, Lady. We don't need speeches.\"",
                    next: "lyra_hums"),
                Node("lyra_hums", "High Priestess Lyra",
                    "(a faint smile) \"Then hear this. The tower... still " +
                    "hums. At night.\"",
                    next: "sylvan_belfry"),
                Node("sylvan_belfry", "Acolyte Sylvan",
                    "\"She means the belfry. The brothers hear it too\u2014a " +
                    "note under the silence, where the dark soot used to ring.\"",
                    next: "eliza_hums"),
                Node("eliza_hums", "Eliza",
                    "(already turning toward the stairwell) \"Hums don't happen " +
                    "for nothing. Come on, {PlayerName}\u2014let's see what your " +
                    "eyes can find up there now.\""));

            // I.3 Whisperwood — "New Leaves, Old Stones".
            Dialogue(InterludesFolder, "D_Interlude_Whisperwood_03", "kaelen_leaves",
                Node("kaelen_leaves", "Ranger Scout Kaelen",
                    "\"Look up, {PlayerName}. New growth since the siphon came " +
                    "out. Another season and you'd never know a blade touched " +
                    "its roots.\"",
                    next: "eliza_dregs"),
                Node("eliza_dregs", "Eliza",
                    "\"And the dregs who drilled it?\"",
                    next: "kaelen_glow"),
                Node("kaelen_glow", "Ranger Scout Kaelen",
                    "\"Gone without a trace, as cultists are. But... there's " +
                    "something odd. Night watch reports a glow under the old " +
                    "altar stones. Cold light, like moonlight trapped in glass.\"",
                    next: "eliza_memory"),
                Node("eliza_memory", "Eliza",
                    "(quietly) \"...I've never stood here before. Why does " +
                    "this place feel like a memory?\"",
                    next: "kaelen_roots"),
                Node("kaelen_roots", "Ranger Scout Kaelen",
                    "\"The forest keeps more than roots, friend.\"",
                    next: "eliza_patient"),
                Node("eliza_patient", "Eliza",
                    "(shaking it off) \"Whatever's under that stone, it's " +
                    "been patient for a very long time. Let's not keep it " +
                    "waiting, partner.\""));
        }

        // --- Helpers -------------------------------------------------------------

        static ClueData LoadClue(string caseFolder, string clueId)
        {
            string path = $"Assets/Data/Cases/{caseFolder}/{clueId}.asset";
            var clue = AssetDatabase.LoadAssetAtPath<ClueData>(path);
            if (clue == null)
                Debug.LogWarning($"HubDialogueContentGenerator: clue '{clueId}' not found " +
                                 $"at '{path}'. registersClue left empty.");
            return clue;
        }

        static DialogueData Dialogue(string folder, string id, string startNodeId,
            params DialogueNodeData[] nodes)
        {
            var data = LoadOrCreate<DialogueData>($"{folder}/{id}.asset");
            data.id = id;
            data.nodes = nodes;
            data.startNodeId = startNodeId;
            EditorUtility.SetDirty(data);
            return data;
        }

        static DialogueNodeData Node(string id, string speaker, string text,
            string next = null, string setsFlag = null, string requiresFlag = null,
            ClueData clue = null)
        {
            return new DialogueNodeData
            {
                id = id,
                speakerName = speaker,
                text = text,
                registersClue = clue,
                setsFlag = setsFlag ?? "",
                requiresFlag = requiresFlag ?? "",
                nextNodeId = next ?? "",
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
