using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Morsoul.Cutscene.Editor
{
    /// <summary>
    /// Authors caption-driven CutsceneData assets. Idempotent: existing assets
    /// at the target paths are loaded and overwritten field-by-field, never
    /// duplicated.
    ///
    /// CAPTION PROVENANCE: sourced from Narrative.md - "In-World Sprite
    /// Cutscene: The Blade and the Optic" (Prologue outro) and opening
    /// cinematic "The Mind Forgets" DRAWN STILL #1-#5. Spoken lines and doc
    /// captions are quoted VERBATIM (marked per shot); beats that exist only as
    /// stage direction are condensed to a single faithful caption line and
    /// marked CONDENSED. Present-day text contains no "Arthur" (his name only
    /// appears inside the pre-cataclysm Elise flashback stills, where the doc
    /// itself uses it).
    ///
    /// Every shot uses the shared placeholder still so CutscenePlanner treats
    /// it as playable until real art exists.
    /// </summary>
    public static class CutsceneContentGenerator
    {
        const string Case00Folder = "Assets/Data/Cutscenes/Case00";
        const string Case05Folder = "Assets/Data/Cutscenes/Case05";
        const string OpeningFolder = "Assets/Data/Cutscenes/Opening";
        const string WhiteSpritePath = "Assets/Sprites/Placeholder/White.png";
        const string StillSpritePath = "Assets/Sprites/Placeholder/Placeholder_Still.png";

        [MenuItem("Morsoul/Data/Generate Case 0 Outro Cutscene")]
        static void GenerateCase0FromMenu()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Generate Case 0 Outro Cutscene",
                "This will (re)generate Assets/Data/Cutscenes/Case00/D_BladeAndOptic.asset, " +
                "overwriting all field values on any existing asset. Continue?",
                "Generate",
                "Cancel");
            if (!proceed) return;

            GenerateCase0Outro();
            ValidateAllCutscenes();
        }

        [MenuItem("Morsoul/Data/Generate Opening Cutscene")]
        static void GenerateOpeningFromMenu()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Generate Opening Cutscene",
                "This will (re)generate Assets/Data/Cutscenes/Opening/D_MindForgets.asset, " +
                "overwriting all field values on any existing asset. Continue?",
                "Generate",
                "Cancel");
            if (!proceed) return;

            GenerateOpening();
            ValidateAllCutscenes();
        }

        /// <summary>
        /// Case 0 outro, "The Blade and the Optic" (Narrative.md Prologue
        /// outro): the Golem leaves the ruins and meets Eliza on the
        /// wilderness trail. Seven shots, one per scene beat.
        /// </summary>
        public static void GenerateCase0Outro()
        {
            var still = EnsurePlaceholderStill();
            var data = Cutscene(Case00Folder, "D_BladeAndOptic", "Hub_Wilderness",
                // Beat [00:00 - Emergence]: stage direction only. CONDENSED.
                Shot(still, "",
                    "Stone grinds on stone. The Golem steps out of the sealed " +
                    "ruin and into daylight, scanning the treeline."),
                // Beat [00:05 - The Ambush]. VERBATIM.
                Shot(still, "Eliza",
                    "Hold it right there, scrap-heap! Hands where I can see them!"),
                // Beat [00:10 - Stand-off]. VERBATIM.
                Shot(still, "Eliza",
                    "Don't play dumb. First-Age constructs don't just dig themselves " +
                    "out of sealed quarantine vaults after three thousand years. " +
                    "Who's your handler? Is the Veiled Circle pulling your strings?!"),
                // Beat [00:22 - De-Escalation]: projected system text. VERBATIM.
                Shot(still, "Golem",
                    "[DIAGNOSTIC BEACON ACTIVE: Unit Type: Forensic & Analytical " +
                    "Assistance Construct. Combat Subroutines: OFFLINE. " +
                    "Core Identity: UNKNOWN.]"),
                // Beat [00:22 - Soul Resonance]: two spoken lines. VERBATIM.
                Shot(still, "Eliza",
                    "Forensic assistance...? You don't have an attack core at all, " +
                    "do you?\n(Murmuring) Wait... that ocular frequency. Why does " +
                    "looking at you make my chest ache...?"),
                // Beat [00:34 - The Key Verification]: two spoken lines. VERBATIM.
                Shot(still, "Eliza",
                    "A First-Age surveyor key? You didn't smash your way through the " +
                    "quarantine blast doors... you solved the manual lock mechanism.\n" +
                    "An ancient machine that solves logic puzzles instead of ripping " +
                    "heads off. If the City Watch catches you lumbering down the " +
                    "Imperial Road, they'll seize you for scrap metal or sell your " +
                    "mana core to the Alchemist Guild."),
                // Beats [00:46 Moniker] + [01:00 Departure]. Lines 1-2 and the
                // name reveal are VERBATIM; line 3 is CONDENSED from the doc's
                // "Lucky for you, my pockets are empty... pull that hood down
                // over your brass plates, keep your mouth shut, and follow me."
                Shot(still, "Eliza",
                    "A First-Age forensic construct with working optical translation... " +
                    "Looks like I just found myself an ancient detective.\n" +
                    "(Quietly, to herself) ...Why did saying that feel so natural?\n" +
                    "Lucky for you, the Investigator's Guild pays bounties for sharp " +
                    "eyes. Name's Eliza Astra. Let's see what Asterveil makes of " +
                    "you... partner."));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"CutsceneContentGenerator: wrote '{AssetDatabase.GetAssetPath(data)}' " +
                      $"({data.shots.Length} shots).");
        }

        /// <summary>
        /// Opening cutscene, "The Mind Forgets" (Narrative.md DRAWN STILL #1-#5):
        /// the pre-cataclysm facility disaster and the Golem's reawakening,
        /// played before PrologueRuins. All five captions are the doc's own
        /// narrative captions, dialogue and system text - VERBATIM.
        /// </summary>
        public static void GenerateOpening()
        {
            var still = EnsurePlaceholderStill();
            var data = Cutscene(OpeningFolder, "D_MindForgets", "PrologueRuins",
                // Still #1 - narrative caption. VERBATIM.
                Shot(still, "",
                    "Before the world shattered into myth and magic, human ambition " +
                    "sought to chain the planet's lifeblood. In the deep dark where " +
                    "truth was buried, two investigators dared to unearth it."),
                // Still #2 - Elise over the override terminal. VERBATIM.
                Shot(still, "Elise",
                    "Arthur! The institute triggered a scorched-earth purge\u2014the " +
                    "resonance cascade is already ripping through the upper bedrock! " +
                    "If we don't dump the reserve charge right now, the containment " +
                    "rupture will vaporize the entire continental plate!"),
                // Still #3 - Elise at the displacement capsule. VERBATIM.
                Shot(still, "Elise",
                    "The displacement field can only shelter one biological pattern. " +
                    "My nervous system can't take the feedback, but your vitals are " +
                    "stable. I'm locking the capsule from out here... You have to " +
                    "wake up, Arthur. Even if everything burns... live."),
                // Still #4 - whiteout narrative caption (doc line break kept).
                // VERBATIM.
                Shot(still, "",
                    "The mind forgets what it cannot bear to hold.\n" +
                    "Yet beneath the dust of thousands of years... the soul remembers."),
                // Still #5 - cyan HUD system text (whitespace-only normalization).
                // VERBATIM.
                Shot(still, "System",
                    ">> CORE RE-INITIALIZATION: OK\n" +
                    ">> TEMPORAL DRIFT ESTIMATE: [CORRUPTED / OVERFLOW]\n" +
                    ">> MEMORY SECTORS 01-89: UNREADABLE (DATA PURGED)\n" +
                    ">> PRIMARY OPTICAL TRANSLATOR: ONLINE\n" +
                    ">> UNIT DESIGNATION: FORENSIC & ANALYTICAL ASSISTANCE CONSTRUCT\n" +
                    ">> CURRENT PROTOCOL: INVESTIGATE / SURVIVE"));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"CutsceneContentGenerator: wrote '{AssetDatabase.GetAssetPath(data)}' " +
                      $"({data.shots.Length} shots).");
        }

        // --- Validation --------------------------------------------------------

        [MenuItem("Morsoul/Data/Generate Finale Cutscenes (Case 5)")]
        static void GenerateFinaleFromMenu()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Generate Finale Cutscenes",
                "This will (re)generate Assets/Data/Cutscenes/Case05/*.asset " +
                "(entry + endings 1-3), overwriting all field values on any " +
                "existing assets. Continue?",
                "Generate",
                "Cancel");
            if (!proceed) return;

            GenerateFinaleCutscenes();
            ValidateAllCutscenes();
        }

        /// <summary>
        /// Case 5 cinematic set (Narrative.md Cutscene 2 "Echoes of the Past"
        /// and Cutscenes 3A/3B/3C). All four are GameFlow-owned: no
        /// nextSceneName so CutsceneEnded drives the board/ending routing.
        /// Spoken lines, narrative captions and telemetry text are quoted
        /// VERBATIM; stage-only beats are condensed (marked CONDENSED).
        /// </summary>
        public static void GenerateFinaleCutscenes()
        {
            var still = EnsurePlaceholderStill();

            // --- Entry: "Echoes of the Past" (deep lift -> Sanctum Core) ------
            Cutscene(Case05Folder, "D_EchoesOfThePast", "",
                // Still #2.1 - narrative caption. VERBATIM.
                Shot(still, "",
                    "Below the bedrock of Asterveil lies the scar that never " +
                    "closed—the point where modern history began, and ancient " +
                    "truth was drowned."),
                // Still #2.2 - Eliza. VERBATIM.
                Shot(still, "Eliza",
                    "{PlayerName}! Steady—what's happening to your core?! " +
                    "The ambient mana down here is tearing your regulators apart!"),
                // Still #2.3 - cyan telemetry flash (doc: 1.2s snap shot). VERBATIM.
                ShotTimed(still, "System",
                    ">> CORE RE-INDEXING: ANOMALY DETECTED\n" +
                    ">> SECTOR 44: [RESONANCE ECHO: \"WE'LL CLOSE THIS CASE TOGETHER\"]\n" +
                    ">> INTEGRITY CHECK: FAILED (MEMORY PURGED)", 1.2f),
                // Still #2.4 - Golem resolve + Eliza (two speakers, split). VERBATIM.
                Shot(still, "Golem",
                    "Core interference stabilized. The ambient frequency does " +
                    "not threaten my systems... it recognizes them. " +
                    "Proceeding to confrontation."),
                Shot(still, "Eliza",
                    "Good. Draw your breath, Detective. The Overseer is right ahead."));

            // --- Ending 1: "The Collapsed Rift" (bad ending) ------------------
            Cutscene(Case05Folder, "D_CollapsedRift", "",
                // Still #3A.1 - red telemetry. VERBATIM.
                Shot(still, "System",
                    ">> FATAL CALIBRATION ERROR: INVERTED FLUX DETECTED\n" +
                    ">> CONTAINMENT FIELD: COLLAPSED\n" +
                    ">> TECTONIC INTEGRITY: 0%"),
                // Still #3A.2 - Eliza. VERBATIM.
                Shot(still, "Eliza",
                    "{PlayerName}! Let go of the console! {PlayerName}—!"),
                // Still #3A.3 - narrative caption (doc line breaks kept). VERBATIM.
                Shot(still, "",
                    "When truth is obscured by error, history repeats its oldest tragedy.\n" +
                    "The rift collapsed. Asterveil fell into the deep.\n" +
                    "And in the cold silence between ages, a shattered consciousness " +
                    "drifted back to sleep... waiting for another dawn."));

            // --- Ending 2: "The Silent Sentinel" (normal ending) --------------
            Cutscene(Case05Folder, "D_SilentSentinel", "",
                // Sprite seq #3B.1 - Guildmaster commendation. VERBATIM.
                Shot(still, "Guildmaster",
                    "By decree of the High Council and the Investigator's Guild, " +
                    "the conspiracy that threatened our realm has been dismantled! " +
                    "Praise be to our keenest minds — hailed as Asterveil's " +
                    "finest investigators!"),
                // Sprite seq #3B.2 - the quiet reflection (4 lines). VERBATIM.
                Shot(still, "Eliza",
                    "We saved them all, {PlayerName}. The mines are secure, the " +
                    "acolyte is free, and the Council is rewriting the trade laws. " +
                    "The gate holds... but the tremor hasn't stopped."),
                Shot(still, "Eliza",
                    "(Gently) Still no memories, huh? Not a single clue about " +
                    "who made you... or who you used to be?"),
                Shot(still, "Golem",
                    "Diagnostic check complete. Memory banks remain permanently " +
                    "unreadable. I am an automaton of the Investigator's Guild. " +
                    "That is sufficient."),
                Shot(still, "Eliza",
                    "Yeah. I guess it has to be. Come on, partner. Night patrol " +
                    "starts in an hour."),
                // Still #3B.3 - closing caption (doc line breaks kept). VERBATIM.
                Shot(still, "",
                    "Justice was defended, and the realm endured.\n" +
                    "Yet beneath the stone faceplate, the past remained an unread " +
                    "book—a faithful sentinel guarding a world whose origins he " +
                    "would never know.\n" +
                    "And far below, the sealed Mana Gate slept: safe for now, but " +
                    "never truly still."));

            // --- Ending 3: "The Hologram in the Dust" (true ending) -----------
            Cutscene(Case05Folder, "D_Ending3_HologramInTheDust", "",
                // Still #3C.1 - cyan telemetry. VERBATIM.
                Shot(still, "System",
                    ">> AUTHORIZATION VERIFIED: DETECTIVE ARTHUR C. DAYLE\n" +
                    ">> STABILIZATION FORMULA: RECONSTRUCTED\n" +
                    ">> MANUAL OVERRIDE REQUIRED: DIRECT NEURAL / CORE ENGAGEMENT"),
                // Still #3C.2 - Eliza then Golem/Arthur (two speakers). VERBATIM.
                Shot(still, "Eliza",
                    "Arthur, no! The voltage will burn out your core! Break the link!"),
                Shot(still, "Arthur",
                    "The containment seal cannot hold without a conscious anchor, " +
                    "Eliza... It was never a machine's duty. It was mine."),
                // Still #3C.3 - memory unveiling monologue. VERBATIM.
                Shot(still, "Arthur",
                    "Three thousand years in the dark... and I remembered the " +
                    "cases, the evidence, the laws...\n" +
                    "But I had forgotten the only truth that ever mattered.\n" +
                    "I remember you, Elise."),
                // Still #3C.4 - optic handoff (two speakers). VERBATIM.
                Shot(still, "Arthur",
                    "Keep this safe... partner. I love you."),
                Shot(still, "Eliza",
                    "I've got it, Arthur. Rest now. The case is closed."),
                // Stills #3C.5-#3C.6 - stage direction only. CONDENSED.
                Shot(still, "",
                    "(Five years later. The Investigator's Guild Historical " +
                    "Archive. On the Chief Investigator's desk, the dormant " +
                    "brass optic core begins to pulse with a warm, steady " +
                    "golden light.)"),
                // Still #3C.7 - hologram exchange (three lines). VERBATIM.
                Shot(still, "Holo-Arthur",
                    "Case file logged, Operative Cochran. Ready to submit to " +
                    "the magistrate?"),
                Shot(still, "Holo-Elise",
                    "Only if you're buying coffee on the way back, Detective."),
                Shot(still, "Holo-Arthur",
                    "(Laughing) Deal."),
                // Still #3C.7 - final caption (doc line breaks kept). VERBATIM.
                Shot(still, "",
                    "Empires fall to ash, and memory fades into myth.\n" +
                    "Yet through catastrophe, stone, and time...\n" +
                    "The soul remembers."));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CutsceneContentGenerator: wrote finale cutscenes to " +
                      $"'{Case05Folder}' (entry + endings 1-3).");
        }

        [MenuItem("Morsoul/Data/Validate Cutscene Assets")]
        public static void ValidateAllCutscenes()
        {
            int errorCount = 0;
            int checkedCount = 0;
            var idOwners = new Dictionary<string, string>(System.StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:CutsceneData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<CutsceneData>(path);
                if (data == null) continue;
                checkedCount++;
                errorCount += Validate(data, path, idOwners);
            }
            if (errorCount == 0)
                Debug.Log($"Validate Cutscene Assets: {checkedCount} CutsceneData asset(s) passed.");
            else
                Debug.LogError($"Validate Cutscene Assets: {errorCount} error(s) across " +
                               $"{checkedCount} CutsceneData asset(s).");
        }

        static int Validate(CutsceneData data, string path,
            Dictionary<string, string> idOwners)
        {
            int errors = 0;
            void Report(string message)
            {
                errors++;
                Debug.LogError($"{path}: {message}", data);
            }

            // Unique, non-empty cutscene id across all assets.
            if (string.IsNullOrEmpty(data.id))
            {
                Report("cutscene id is empty");
            }
            else if (idOwners.TryGetValue(data.id, out string owner))
            {
                Report($"duplicate cutscene id '{data.id}' (also used by {owner})");
            }
            else
            {
                idOwners.Add(data.id, path);
            }

            // Shots: non-empty, non-null; visual-less shots are skipped by the
            // planner so they are warnings, not errors.
            if (data.shots == null || data.shots.Length == 0)
            {
                Report("shots array is empty");
            }
            else
            {
                for (int i = 0; i < data.shots.Length; i++)
                {
                    var shot = data.shots[i];
                    if (shot == null)
                    {
                        Report($"shots[{i}] is null");
                        continue;
                    }
                    if (!CutscenePlanner.HasVisual(shot))
                        Debug.LogWarning($"{path}: shots[{i}] has neither image nor " +
                                         "video and will be skipped by the planner.", data);
                    if (!shot.waitForInput && shot.durationSeconds <= 0f)
                        Debug.LogWarning($"{path}: shots[{i}] advances instantly " +
                                         "(waitForInput off, durationSeconds <= 0).", data);
                }
            }

            // Scene handoff: empty is legitimate for GameFlow-owned cutscenes
            // (CutsceneEnded drives routing instead of nextSceneName), so this
            // is a warning rather than an error.
            if (string.IsNullOrEmpty(data.nextSceneName))
            {
                Debug.LogWarning($"{path}: nextSceneName is empty - valid only " +
                                 "when a scene composer (e.g. GameFlow) owns routing.", data);
            }
            else if (!SceneInBuildSettings(data.nextSceneName))
            {
                Debug.LogWarning($"{path}: nextSceneName '{data.nextSceneName}' is not " +
                                 "in EditorBuildSettings.scenes.", data);
            }

            return errors;
        }

        static bool SceneInBuildSettings(string sceneName)
        {
            foreach (var scene in EditorBuildSettings.scenes)
                if (string.Equals(Path.GetFileNameWithoutExtension(scene.path),
                        sceneName, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        // --- Helpers -----------------------------------------------------------

        static CutsceneData Cutscene(string folder, string id, string nextSceneName,
            params CutsceneShotData[] shots)
        {
            EnsureFolder(folder);
            var data = LoadOrCreate<CutsceneData>($"{folder}/{id}.asset");
            data.id = id;
            data.shots = shots;
            data.nextSceneName = nextSceneName;
            data.skipToScene = true;
            EditorUtility.SetDirty(data);
            return data;
        }

        static CutsceneShotData Shot(Sprite image, string speaker, string caption)
        {
            return new CutsceneShotData
            {
                image = image,
                video = null,
                durationSeconds = 6f,
                waitForInput = true,
                caption = caption,
                captionSpeaker = speaker ?? "",
            };
        }

        /// <summary>Auto-advancing shot for timed beats (e.g. telemetry flashes).</summary>
        static CutsceneShotData ShotTimed(Sprite image, string speaker, string caption,
            float durationSeconds)
        {
            return new CutsceneShotData
            {
                image = image,
                video = null,
                durationSeconds = durationSeconds,
                waitForInput = false,
                caption = caption,
                captionSpeaker = speaker ?? "",
            };
        }

        /// <summary>
        /// Placeholder still for caption shots: reuses the hub factory's White.png
        /// when present, otherwise generates a 1920x1080 near-black sprite once
        /// at Assets/Sprites/Placeholder/Placeholder_Still.png (mirrors
        /// HubSceneFactory.EnsureWhiteSprite).
        /// </summary>
        static Sprite EnsurePlaceholderStill()
        {
            var white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            if (white != null) return white;

            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(StillSpritePath);
            if (existing != null) return existing;

            EnsureFolder("Assets/Sprites/Placeholder");
            const int width = 1920, height = 1080;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[width * height];
                var fill = new Color32(10, 10, 16, 255); // near-black, cool tint
                for (int i = 0; i < pixels.Length; i++) pixels[i] = fill;
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(StillSpritePath, ImageConversion.EncodeToPNG(texture));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(StillSpritePath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(StillSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(StillSpritePath);
            if (sprite == null)
                Debug.LogError($"CutsceneContentGenerator: failed to create sprite at " +
                               $"'{StillSpritePath}'.");
            return sprite;
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
