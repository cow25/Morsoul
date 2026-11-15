using System;
using Morsoul.Core;
using Morsoul.Dialogue;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Context-sensitive hub conversation point for Eliza Astra. Attached at runtime
    /// by <see cref="ElizaHubPresenceController"/> when Eliza is either stationed
    /// at her own investigation lead or accompanying the player in the field.
    /// </summary>
    public class ElizaTalkInteractable : Interactable2D
    {
        public string hubSceneName;

        protected override bool BlocksMovement => blocksMovement;

        void Reset()
        {
            promptText = "[E] Talk";
        }

        public override void Interact(HubContext ctx)
        {
            base.Interact(ctx);
            if (ctx == null) return;
            var session = CoreRuntime.Instance != null ? CoreRuntime.Instance.Session : null;
            if (session == null) return;

            var data = BuildConversation(hubSceneName, session);
            if (data != null) ctx.PlayDialogue(data);
        }

        static DialogueData BuildConversation(string sceneName, SessionState session)
        {
            int c = session.CurrentCase;

            if (string.Equals(sceneName, "Hub_Guild", StringComparison.OrdinalIgnoreCase))
                return BuildGuildTalk(session, c);
            if (string.Equals(sceneName, "Hub_LowerMarket", StringComparison.OrdinalIgnoreCase))
                return BuildMarketTalk(session, c);
            if (string.Equals(sceneName, "Hub_CathedralNave", StringComparison.OrdinalIgnoreCase))
                return BuildCathedralTalk(session, c);
            if (string.Equals(sceneName, "Hub_Wilderness", StringComparison.OrdinalIgnoreCase))
                return BuildWhisperwoodTalk(session, c);
            if (string.Equals(sceneName, "Hub_IronHollow", StringComparison.OrdinalIgnoreCase))
                return BuildIronHollowTalk(session, c);

            return MakeChain("D_Eliza_Generic",
                ("Eliza", "Keep your lens sharp, {PlayerName}. Every clue we pull tightens the noose."));
        }

        static DialogueData BuildGuildTalk(SessionState session, int currentCase)
        {
            if (currentCase <= 1)
            {
                if (!session.HasFlag(GuildIntroRules.WarrantFlag))
                {
                    return MakeChain("D_Eliza_Guild_C1Pre",
                        ("Eliza", "We've got our Communicator Crystals synced now. Head upstairs to finish Ovan's Case 1 dossier so we can grab the cellar warrant."),
                        ("Eliza", "I'll keep an eye on the Quest Board down here—and ask around the Guild tavern about who's been moving dark reagents."));
                }
                return MakeChain("D_Eliza_Guild_C1Post",
                    ("Eliza", "Warrant's in hand. Head out to the Lower Market when you're ready—I'll slip ahead and shake down the alley fences near the cellar grate."),
                    ("Eliza", "If you find anything odd inside that vault, my Crystal will chime right away."));
            }

            if (currentCase <= 4)
            {
                if (session.HasFlag($"Guild.CheckBoard.{currentCase}"))
                {
                    return MakeChain($"D_Eliza_Guild_CheckBoard_C{currentCase}",
                        ("Eliza", "The new dispatch is right there on the Quest Board. Take a look when you're ready, and we'll head to the Carrier together."));
                }
                return MakeChain($"D_Eliza_Guild_Joined_C{currentCase}",
                    ("Eliza", "Take the door back out to the Lower Market whenever you're ready—I'll guide you straight to the Carrier wagon."));
            }

            int logs = session.RunicLogCount;
            if (logs < 4)
            {
                return MakeChain("D_Eliza_Guild_C5Warn",
                    ("Eliza", $"Case after case, we've been staring at pieces of one giant lock. With your Aether Sight active, we've recovered {logs} of 4 dormant Runic Logs so far."),
                    ("Eliza", "If you want to revisit the Lower Market vault, the Cathedral belfry, or Whisperwood Grove via the Carrier before taking the Iron Hollow lift into the Sanctum, I'm right behind you."));
            }

            return MakeChain("D_Eliza_Guild_C5Ready",
                ("Eliza", "All four Runic Logs translated, and every thread from the Vault to Drift 09 points straight into Iron Hollow's abyss. Whenever you're ready, let's take the Carrier to the Sanctum lift."));
        }

        static DialogueData BuildMarketTalk(SessionState session, int currentCase)
        {
            if (currentCase <= 1)
            {
                if (session.HasClue("C3_01") || session.HasClue("C1_C3"))
                {
                    return MakeChain("D_Eliza_Market_Solvent",
                        ("Eliza", "I leaned on three back-alley apothecary fences between here and the wharf. Not a single licensed brewer in Asterveil touches solvent like that."),
                        ("Eliza", "Funny, though... five minutes ago you were dust under overgrown stone, and already we finish each other's deductions like old partners."));
                }
                return MakeChain("D_Eliza_Market_FenceSweep",
                    ("Eliza", "While you press Ignatius in his parlor and sweep the subterranean vault, I'm tracing which smugglers move unregulated alchemy down these alleys."),
                    ("Eliza", "Holler—or keep your Crystal close—if anything beneath the grate smells wrong."));
            }

            if (currentCase >= 5)
            {
                return MakeChain("D_Eliza_Market_Revisit",
                    ("Eliza", "Seeing the Lower Market calm again feels strange after everything since that first vault fire."),
                    ("Eliza", "Remember the lower potion-rack alcove beneath the grate—if your Aether Sight spots a faint First-Age mark down there, let's pull every last line of it."));
            }

            return MakeChain("D_Eliza_Market_Transit",
                ("Eliza", "The Carrier's waiting by the west gate whenever you want to ride back out to the active district."));
        }

        static DialogueData BuildCathedralTalk(SessionState session, int currentCase)
        {
            if (currentCase == 2)
            {
                if (!session.HasFlag("C2_BelfryKeyGranted"))
                {
                    return MakeChain("D_Eliza_Cath_Kenneth",
                        ("Eliza", "Valerius has Templars watching the altar, so I slipped through the cloister to question Brother Kenneth directly."),
                        ("Eliza", "He swore by Saint Althea's name that the Spirit-folk acolyte never stopped pouring oil within arm's reach during all six bells. Accord's story doesn't hold water—press him for the belfry key."));
                }
                return MakeChain("D_Eliza_Cath_Headache",
                    ("Eliza", "Got the belfry key off Accord? Good... Wait—"),
                    ("Eliza", "(Presses two fingers against her temple) That dull metallic hum drifting down from the bell tower... Does it make your head ring, {PlayerName}?"),
                    ("Eliza", "For half a heartbeat, it sounded just like a warning bell echoing down a stark white stone corridor. Shake it off—let's see what's bolted up in those rafters."));
            }

            if (currentCase >= 5)
            {
                return MakeChain("D_Eliza_Cath_Revisit",
                    ("Eliza", "Mother Lyra says the tower beam still hums faintly after midnight—even with the poison scrubbed clean. Let your new eyes sweep the upper rafters if we haven't deciphered that slab yet."));
            }

            return MakeChain("D_Eliza_Cath_Idle",
                ("Eliza", "Good to see Sylvan walking the nave unchained. At least one injustice in this city actually got put right."));
        }

        static DialogueData BuildWhisperwoodTalk(SessionState session, int currentCase)
        {
            if (currentCase == 3)
            {
                if (!session.HasFlag("C3_PerimeterSealGranted"))
                {
                    return MakeChain("D_Eliza_Wild_Shield",
                        ("Eliza", "I'm staying right on your flank in these woods, partner. Half the grove spirits have gone mad from the blight, and an investigator's lens can't parry claws on its own."),
                        ("Eliza", "Let's hear Ranger Kaelen out—and see why an academic 'botanist' is sitting beside heavy iron barrels."));
                }
                return MakeChain("D_Eliza_Wild_Altar",
                    ("Eliza", "We have Kaelen's Perimeter Seal. Watch your footing past the briar gate—and stay close when we corner Vane."),
                    ("Eliza", "(Quietly) Every step closer to that old altar stone makes my ribs tighten, like walking across a grave I forgot I dug."));
            }

            if (currentCase >= 5)
            {
                return MakeChain("D_Eliza_Wild_Revisit",
                    ("Eliza", "Look at the fresh silver-green shoots along the roots... Kaelen says the cold light under the altar stone still pulses at night. If you haven't read it with Aether Sight yet, let's uncover it."));
            }

            return MakeChain("D_Eliza_Wild_Idle",
                ("Eliza", "The air out here tastes cleaner than Asterveil's cobblestones, at least."));
        }

        static DialogueData BuildIronHollowTalk(SessionState session, int currentCase)
        {
            if (currentCase <= 4)
            {
                if (!session.HasFlag("C4_OverrideClearance"))
                {
                    return MakeChain("D_Eliza_Iron_Manifests",
                        ("Eliza", "While you talk to Overseer Thrun, I traced the railhead cargo tallies behind the winch house."),
                        ("Eliza", "Three sealed lead casks carrying the exact pine-pitch stamp from Vane's camp were routed down to Drift 09 under contractor Harlan's signature. Get Thrun's override key and we'll follow that trail below."));
                }
                return MakeChain("D_Eliza_Iron_DeepDrift",
                    ("Eliza", "We've got the deep-shaft clearance. Watch for loose blasting charges down in Drift 09—Harlan looked far too eager to blame Chief Brokk before anyone checked the rock."));
            }

            int logs = session.RunicLogCount;
            if (logs < 4)
            {
                return MakeChain("D_Eliza_Iron_PreSanctum",
                    ("Eliza", $"The Grand Overseer already rode the freight cage down into the Sanctum Core. Your optics show {logs} of 4 First-Age Runic Logs deciphered across the city."),
                    ("Eliza", "The Carrier back in the plaza can take us to Lower Market, Cathedral Nave, or Whisperwood if you want every last piece of the past before we drop down."));
            }

            return MakeChain("D_Eliza_Iron_AllLogs",
                ("Eliza", "Four out of four Runic Logs unlocked, and the Sanctum freight lift is waiting right beside us. Ready to close the last case together, partner?"));
        }

        static DialogueData MakeChain(string id, params (string speaker, string text)[] lines)
        {
            var data = ScriptableObject.CreateInstance<DialogueData>();
            data.hideFlags = HideFlags.DontSave;
            data.id = id;
            data.startNodeId = "n0";
            data.nodes = new DialogueNodeData[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                data.nodes[i] = new DialogueNodeData
                {
                    id = $"n{i}",
                    speakerName = lines[i].speaker,
                    text = lines[i].text,
                    nextNodeId = i + 1 < lines.Length ? $"n{i + 1}" : ""
                };
            }
            return data;
        }
    }
}

