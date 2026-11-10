using Morsoul.Data;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Dialogue.Editor
{
    /// <summary>
    /// Authors the prologue -> Lower Market arrival and first Guild-visit
    /// dialogue assets. Idempotent like <see cref="HubDialogueContentGenerator"/>:
    /// existing assets are loaded and overwritten field-by-field. Lines follow
    /// the technological-literacy canon — inhabitants frame the unfamiliar
    /// through a medieval lens and nobody identifies the Golem's nature.
    /// </summary>
    public static class GuildIntroContentGenerator
    {
        public const string Folder = "Assets/Data/Dialogue/Prologue";

        [MenuItem("Morsoul/Data/Generate Guild Intro Dialogue (Arrival + First Visit)")]
        public static void Generate()
        {
            EnsureFolder(Folder);

            // Market arrival — Eliza guiding the Golem in from the west entry.
            Dialogue("D_Market_Arrival", "arrival_1",
                Node("arrival_1", "Eliza",
                    "\"This way, {PlayerName}. Welcome to the Lower Market \u2014 " +
                    "half of Asterveil's coin changes hands down here.\"",
                    next: "arrival_2"),
                Node("arrival_2", "Eliza",
                    "\"Keep your head up. The crowd looks rough, but the Guild " +
                    "keeps the worst of it honest.\""));

            // At the Guild hall door.
            Dialogue("D_Guild_Door", "door_1",
                Node("door_1", "Eliza",
                    "\"There \u2014 the big hall with the banners. That's the " +
                    "Guild. Every contract and investigation in the city " +
                    "crosses their desk.\"",
                    next: "door_2"),
                Node("door_2", "Eliza",
                    "\"Stay close. The Guildmaster can be gruff \u2014 let me " +
                    "do the talking.\""));

            // Front desk greeting — the clerk meets the Golem.
            Dialogue("D_Guild_Reception", "eliza_desk",
                Node("eliza_desk", "Eliza",
                    "\"Front desk is Marta \u2014 the Guild clerk. Every open " +
                    "job in Asterveil passes her board. Best eyes in the city " +
                    "for who's hiring.\"",
                    next: "marta_shock"),
                Node("marta_shock", "Marta",
                    "\"Back from the ruins already, Eliza? And you've " +
                    "brought\u2014 oh. Oh my. What in the Saints' name is " +
                    "THAT?\"",
                    next: "eliza_vouch"),
                Node("eliza_vouch", "Eliza",
                    "\"A friend, Marta. A very strange friend. We're going " +
                    "up to see Ovan.\"",
                    next: "marta_stairs"),
                Node("marta_stairs", "Marta",
                    "\"Well\u2014 he's in his office, upstairs at the end of " +
                    "the hall. Do try not to let it break anything.\""));

            // Ovan's office — the Guildmaster introduction. Deliberately
            // grants nothing: the mission stays on Ovan's ordinary talk
            // interaction afterwards.
            Dialogue("D_Ovan_Meeting", "ovan_look",
                Node("ovan_look", "Guildmaster Ovan",
                    "\"(Looks up from a desk buried in ledgers) Eliza. Back " +
                    "from the ruins already \u2014 and you've brought company.\"",
                    next: "eliza_found"),
                Node("eliza_found", "Eliza",
                    "\"Found them at the ruins, Ovan. They're no monster " +
                    "\u2014 give them a chance before you call the guard.\"",
                    next: "ovan_weigh"),
                Node("ovan_weigh", "Guildmaster Ovan",
                    "\"Hrm. Never seen your like on any registry, friend " +
                    "\u2014 and I've signed every oddity this city's coughed " +
                    "up in twenty years. But Eliza's nose for trouble is " +
                    "sharper than my quill.\"",
                    next: "ovan_offer"),
                Node("ovan_offer", "Guildmaster Ovan",
                    "\"The Guild takes all kinds. When you're ready for " +
                    "honest work, my door stays open.\""));

            // Ambient adventurers on the guild floor.
            Dialogue("D_GuildBarks", "bark_1",
                Node("bark_1", "Scarred Adventurer",
                    "Heard the old ruins past the woods opened up last night. " +
                    "Nobody's walked out of there in living memory \u2014 " +
                    "whatever came out, I don't want its contract.",
                    next: "bark_2"),
                Node("bark_2", "Young Adventurer",
                    "New faces at the desk again. Guild's been busy since " +
                    "the vault business \u2014 good. More work for the rest " +
                    "of us."));

            // The clerk's standing bark after the introduction.
            Dialogue("D_GuildClerk", "clerk_1",
                Node("clerk_1", "Marta",
                    "The board's there if you want work, dear. Ovan handles " +
                    "the bigger contracts himself \u2014 upstairs."));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("GuildIntroContentGenerator: 6 dialogue assets written under " + Folder);
        }

        // --- Helpers (mirrors HubDialogueContentGenerator) -------------------

        static DialogueData Dialogue(string id, string startNodeId,
            params DialogueNodeData[] nodes)
        {
            var data = LoadOrCreate<DialogueData>($"{Folder}/{id}.asset");
            data.id = id;
            data.nodes = nodes;
            data.startNodeId = startNodeId;
            EditorUtility.SetDirty(data);
            return data;
        }

        static DialogueNodeData Node(string id, string speaker, string text,
            string next = null)
        {
            return new DialogueNodeData
            {
                id = id,
                speakerName = speaker,
                text = text,
                setsFlag = "",
                requiresFlag = "",
                nextNodeId = next ?? "",
            };
        }

        static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folderPath));
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
