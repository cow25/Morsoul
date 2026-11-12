using Morsoul.Core;
using Morsoul.Hub;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Morsoul.Tests
{
    public class PrologueTransitionTests
    {
        SessionState _session;

        [SetUp]
        public void SetUp() => _session = new SessionState();

        [Test]
        public void ElizaIntro_RequiresExitLoaded_Unseen_AndNoActiveCutscene()
        {
            Assert.IsFalse(PrologueTransitionRules.CanTriggerElizaIntro(_session));

            _session.PrologueExitLoaded = true;
            Assert.IsTrue(PrologueTransitionRules.CanTriggerElizaIntro(_session));

            _session.SetCutsceneContext(SessionState.ElizaIntroCutsceneId,
                SessionState.ElizaPreContactCheckpointId);
            Assert.IsFalse(PrologueTransitionRules.CanTriggerElizaIntro(_session));

            _session.SetCutsceneContext("", "");
            _session.ElizaIntroSeen = true;
            Assert.IsFalse(PrologueTransitionRules.CanTriggerElizaIntro(_session));
        }

        [Test]
        public void IntroLine_UsesNpcRunner_AndWaitsForAdvance()
        {
            var go = new GameObject("IntroTest", typeof(BoxCollider2D));
            var npc = ScriptableObject.CreateInstance<Morsoul.Dialogue.DialogueData>();
            npc.id = "NPC_TEST";
            npc.startNodeId = "hello";
            npc.nodes = new[]
            {
                new Morsoul.Dialogue.DialogueNodeData { id = "hello", speakerName = "NPC", text = "Hello." },
            };
            try
            {
                var dialogue = go.AddComponent<Morsoul.Dialogue.DialogueController>();
                dialogue.manageCursor = false;
                var context = go.AddComponent<HubContext>();
                context.dialogue = dialogue;
                dialogue.SetDialogue(npc);
                var intro = go.AddComponent<PrologueElizaIntroController>();
                intro.context = context;
                var method = typeof(PrologueElizaIntroController).GetMethod("Line",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var line = (System.Collections.IEnumerator)method.Invoke(intro,
                    new object[] { "Golem", "Can you understand me?", null });

                Assert.IsTrue(line.MoveNext());
                Assert.IsNotNull(dialogue.Runner);
                Assert.AreEqual("Can you understand me?", dialogue.Runner.Current.text);
                Assert.IsTrue(line.MoveNext());
                Assert.IsTrue(dialogue.IsPlaying);
                dialogue.Runner.Advance();
                Assert.IsFalse(line.MoveNext());
                Assert.IsFalse(dialogue.IsPlaying);
                Assert.AreSame(npc, dialogue.dialogueData);
                Assert.IsTrue(context.PlayDialogue(npc));
                Assert.AreEqual("Hello.", dialogue.Runner.Current.text);
                Assert.IsTrue(dialogue.IsPlaying);
                dialogue.Runner.Advance();
                Assert.IsFalse(dialogue.IsPlaying);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(npc);
            }
        }

        [Test]
        public void Encounter_OnlyElizasFirstWarningIsCorrupted_AndSpeechWaitsForInput()
        {
            var go = new GameObject("EncounterContentTest", typeof(BoxCollider2D));
            try
            {
                var dialogue = go.AddComponent<Morsoul.Dialogue.DialogueController>();
                dialogue.manageCursor = false;
                var context = go.AddComponent<HubContext>();
                context.dialogue = dialogue;
                var intro = go.AddComponent<PrologueElizaIntroController>();
                intro.context = context;
                var sequence = (System.Collections.IEnumerator)typeof(PrologueElizaIntroController)
                    .GetMethod("RunSequence", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(intro, new object[] { _session });
                int corrupted = 0;
                bool deciphered = false;
                bool finished = false;
                bool plea = false, monster = false, noWeapons = false;
                bool introduced = false;
                while (!finished && sequence.MoveNext())
                {
                    var line = sequence.Current as System.Collections.IEnumerator;
                    if (line == null || !line.GetType().Name.StartsWith("<Line>")) continue;
                    try
                    {
                        Assert.IsTrue(line.MoveNext());
                        var node = dialogue.Runner.Current;
                        if (node.text.Contains("\u2593"))
                        {
                            corrupted++;
                            Assert.AreEqual("???", node.speakerName);
                            Assert.IsFalse(deciphered);
                        }
                        Assert.IsFalse(node.text.Contains("Relics"));
                        Assert.IsFalse(node.text.Contains("don't want to hurt"));
                        Assert.IsFalse(node.text.Contains("do not remember"));
                        Assert.IsFalse(node.text.Contains("you can speak"));
                        Assert.AreNotEqual("System", node.speakerName);
                        // Speaker identity follows narrative knowledge: her
                        // name only appears after she introduces herself.
                        if (node.text.Contains("Name's Eliza"))
                        {
                            Assert.AreEqual("???", node.speakerName);
                            introduced = true;
                        }
                        if (node.speakerName == "Eliza")
                            Assert.IsTrue(introduced,
                                $"'Eliza' label shown before her introduction: '{node.text}'");
                        if (node.text.Contains("words in the notes")) deciphered = true;
                        if (node.text == "Wait. I mean no harm.") Assert.IsTrue(deciphered);
                        plea |= node.text == "Look, wait! Don't hurt me!" && !deciphered;
                        monster |= node.text == "Why should I listen to a monster like you?"
                                   && deciphered && node.speakerName == "???";
                        noWeapons |= node.text == "I have no weapons on me! At least hear me out first!"
                                     && deciphered;
                        finished = node.text == "I'll answer your questions along the way. We've got time.";
                        Assert.IsTrue(line.MoveNext());
                        dialogue.Runner.Advance();
                        Assert.IsFalse(line.MoveNext());
                    }
                    finally
                    {
                        (line as System.IDisposable)?.Dispose();
                    }
                }
                Assert.IsTrue(finished);
                Assert.AreEqual(1, corrupted);
                Assert.IsTrue(plea && monster && noWeapons);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Encounter_ScanDeactivatesWhenElizaEmerges_AndExitReleasesCameraFollow()
        {
            var go = new GameObject("ScanAndCameraTest", typeof(BoxCollider2D));
            var highlightGo = new GameObject("EyeHighlight", typeof(EyeScanPulse2D));
            var bushGo = new GameObject("ElizaCover");
            var cameraGo = new GameObject("IntroCamera", typeof(Camera), typeof(CameraFollow2D));
            var playerGo = new GameObject("Player", typeof(HubPlayerController));
            try
            {
                var dialogue = go.AddComponent<Morsoul.Dialogue.DialogueController>();
                dialogue.manageCursor = false;
                var context = go.AddComponent<HubContext>();
                context.dialogue = dialogue;
                var intro = go.AddComponent<PrologueElizaIntroController>();
                intro.context = context;
                intro.player = playerGo.GetComponent<HubPlayerController>();
                intro.cameraFollow = cameraGo.GetComponent<CameraFollow2D>();
                intro.bushProp = bushGo.transform;
                intro.eyeHighlight = highlightGo.transform;

                var sequence = (System.Collections.IEnumerator)typeof(PrologueElizaIntroController)
                    .GetMethod("RunSequence", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(intro, new object[] { _session });

                bool scanActiveOnNotice = false;
                bool scanOffOnWarning = false;
                bool exitReached = false;
                bool cameraUnfollowedOnExit = false;

                while (sequence.MoveNext())
                {
                    if (exitReached)
                    {
                        cameraUnfollowedOnExit = intro.cameraFollow.target == null;
                        break;
                    }

                    var line = sequence.Current as System.Collections.IEnumerator;
                    if (line == null || !line.GetType().Name.StartsWith("<Line>")) continue;
                    try
                    {
                        Assert.IsTrue(line.MoveNext());
                        var node = dialogue.Runner.Current;
                        if (node.text == "Someone is hiding in that bush.")
                            scanActiveOnNotice = highlightGo.activeSelf;
                        if (node.text.Contains("\u2593"))
                            scanOffOnWarning = !highlightGo.activeSelf;
                        if (node.text == "I'll answer your questions along the way. We've got time.")
                            exitReached = true;

                        Assert.IsTrue(line.MoveNext());
                        dialogue.Runner.Advance();
                        Assert.IsFalse(line.MoveNext());
                    }
                    finally
                    {
                        (line as System.IDisposable)?.Dispose();
                    }
                }

                Assert.IsTrue(scanActiveOnNotice, "Eye scan should be active when noticing someone in the bush.");
                Assert.IsTrue(scanOffOnWarning, "Eye scan should turn off before Eliza's warning line once she emerges.");
                Assert.IsTrue(exitReached && cameraUnfollowedOnExit, "CameraFollow2D.target must be cleared during the exit walkout.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(highlightGo);
                Object.DestroyImmediate(bushGo);
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(playerGo);
            }
        }

        [Test]
        public void StageEncounter_KeepsPlayerAndCamera_StagesElizaSurveying()
        {
            var cameraGo = new GameObject("IntroCamera", typeof(Camera), typeof(CameraFollow2D));
            var playerGo = new GameObject("Player");
            var elizaGo = new GameObject("Eliza");
            var survey = new GameObject("ElizaSurveySpot");
            var trigger = new GameObject("IntroTrigger", typeof(BoxCollider2D));
            try
            {
                elizaGo.SetActive(false);
                survey.transform.position = new Vector3(14.1f, 0.4f, 0f);
                playerGo.transform.position = new Vector3(2.8f, 0f, 0f);
                var intro = trigger.AddComponent<PrologueElizaIntroController>();
                intro.player = playerGo.AddComponent<HubPlayerController>();
                intro.cameraFollow = cameraGo.GetComponent<CameraFollow2D>();
                intro.eliza = elizaGo.transform;
                intro.elizaSurveyMarker = survey.transform;

                typeof(PrologueElizaIntroController).GetMethod("StageEncounter",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(intro, null);

                // The protagonist is never teleported or hidden — the scripted
                // walk continues from wherever the trigger caught him.
                Assert.AreEqual(new Vector3(2.8f, 0f, 0f), playerGo.transform.position);
                Assert.AreEqual(playerGo.transform, intro.cameraFollow.target);
                Assert.AreEqual(playerGo.transform.position.x,
                    cameraGo.transform.position.x, 0.001f);
                // Eliza is already in-world surveying, off the right edge.
                Assert.IsTrue(elizaGo.activeSelf);
                Assert.AreEqual(survey.transform.position, elizaGo.transform.position);
            }
            finally
            {
                foreach (var go in new[] { trigger, playerGo, cameraGo, elizaGo, survey })
                    Object.DestroyImmediate(go);
            }
        }

        [TestCase(1.333333f)]
        [TestCase(1.777778f)]
        [TestCase(2.333333f)]
        public void Trigger_FiresJustBeforeBushEntersFrame(float aspect)
        {
            var cameraGo = new GameObject("IntroCamera", typeof(Camera), typeof(CameraFollow2D));
            var playerGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var bushGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var trigger = new GameObject("IntroTrigger", typeof(BoxCollider2D));
            try
            {
                var camera = cameraGo.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.aspect = aspect;
                bushGo.transform.position = new Vector3(12.7f, 0.2f, 0f);
                bushGo.transform.localScale = new Vector3(1.2f, 1.4f, 1f);
                trigger.transform.position = new Vector3(8f, -0.5f, 0f);
                var intro = trigger.AddComponent<PrologueElizaIntroController>();
                intro.player = playerGo.AddComponent<HubPlayerController>();
                intro.cameraFollow = cameraGo.GetComponent<CameraFollow2D>();
                intro.bushProp = bushGo.transform;

                intro.PositionTriggerForReveal();

                var col = trigger.GetComponent<Collider2D>();
                float bushLeft = bushGo.GetComponent<Renderer>().bounds.min.x;
                float playerHalf = playerGo.GetComponent<Renderer>().bounds.extents.x;
                float contactX = col.bounds.min.x - playerHalf;
                float rightEdgeAtContact = contactX + 5f * aspect;
                Assert.LessOrEqual(trigger.transform.position.x, intro.triggerMaxX);
                Assert.Less(rightEdgeAtContact, bushLeft,
                    "Bush already on screen when the trigger fires");
                Assert.Greater(rightEdgeAtContact, bushLeft - 2f,
                    "Trigger sits implausibly far before the reveal");
            }
            finally
            {
                foreach (var go in new[] { trigger, playerGo, cameraGo, bushGo })
                    Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FadeTo_NeverRevealsDialogueBox()
        {
            var go = new GameObject("FadeTest");
            try
            {
                var dialogue = go.AddComponent<Morsoul.Dialogue.DialogueController>();
                dialogue.manageCursor = false;
                var tree = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(
                    "Assets/Dialogue.uxml").CloneTree();
                typeof(Morsoul.Dialogue.DialogueController).GetMethod("OnUIReload",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(dialogue, new object[] { null, tree, 1 });

                var root = tree.Q<UnityEngine.UIElements.VisualElement>("Dialogue-Root");
                var overlay = tree.Q<UnityEngine.UIElements.VisualElement>("FadeOverlay");
                Assert.IsNotNull(overlay);
                Assert.IsNull(root.Q("FadeOverlay"),
                    "FadeOverlay must live outside Dialogue-Root so fades don't expose the box");

                var fade = dialogue.FadeTo(1f, 0.1f);
                fade.MoveNext();
                Assert.AreEqual(UnityEngine.UIElements.DisplayStyle.None, root.style.display.value,
                    "Fading must not reveal the dialogue box/template");
                Assert.AreEqual(UnityEngine.UIElements.DisplayStyle.Flex, overlay.style.display.value);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NamePrompt_ReopensPanelAfterDialogueEnds()
        {
            var go = new GameObject("NamePromptTest");
            try
            {
                var dialogue = go.AddComponent<Morsoul.Dialogue.DialogueController>();
                dialogue.manageCursor = false;
                var tree = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(
                    "Assets/Dialogue.uxml").CloneTree();
                typeof(Morsoul.Dialogue.DialogueController).GetMethod("OnUIReload",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(dialogue, new object[] { null, tree, 1 });
                dialogue.Stop();
                Assert.IsTrue(dialogue.ShowNamePrompt());
                Assert.IsTrue(dialogue.IsPlaying);
                Assert.AreEqual(UnityEngine.UIElements.DisplayStyle.Flex,
                    tree.Q<UnityEngine.UIElements.VisualElement>("Dialogue-Root").style.display.value);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RuinsGate_RequiresSolvedBoard_AndGateUnlock()
        {
            Assert.IsFalse(PrologueTransitionRules.CanCrossRuinsGate(_session, 0));

            _session.GetOrCreateBoardState(0).BoardSolved = true;
            Assert.IsFalse(PrologueTransitionRules.CanCrossRuinsGate(_session, 0));

            _session.PrologueGateUnlocked = true;
            Assert.IsTrue(PrologueTransitionRules.CanCrossRuinsGate(_session, 0));
            Assert.IsFalse(PrologueTransitionRules.CanCrossRuinsGate(_session, 1));
        }

        [Test]
        public void InterruptedElizaIntro_RestoresPreContactState()
        {
            _session.PrologueExitLoaded = true;
            _session.ElizaIntroTriggered = true;
            _session.EasternRouteUnlocked = true;
            _session.SetCutsceneContext(SessionState.ElizaIntroCutsceneId,
                SessionState.ElizaPreContactCheckpointId);
            _session.SetPlayerContext(SceneMode.Mode2D,
                SessionState.PrologueWildernessSceneId,
                new Vector3(12f, 0f, 0f), PlayerFacing2D.Left);

            Assert.IsTrue(_session.ResumeInterruptedElizaIntro());

            Assert.IsTrue(_session.PrologueExitLoaded);
            Assert.IsFalse(_session.ElizaIntroTriggered);
            Assert.IsFalse(_session.ElizaIntroSeen);
            Assert.IsFalse(_session.EasternRouteUnlocked);
            Assert.AreEqual("", _session.ActiveCutsceneId);
            Assert.AreEqual("", _session.PreCutsceneCheckpointId);
            Assert.AreEqual(SceneMode.Mode2D, _session.CurrentSceneMode);
            Assert.AreEqual(SessionState.PrologueWildernessSceneId, _session.CurrentSceneId);
            Assert.AreEqual(new Vector3(1.5f, 0f, 0f), _session.PlayerPosition);
            Assert.AreEqual(PlayerFacing2D.Right, _session.PlayerFacing2D);
            Assert.IsTrue(PrologueTransitionRules.CanTriggerElizaIntro(_session));
        }

        [Test]
        public void InterruptedElizaIntro_DoesNotRewindCompletedOrUnrelatedCutscene()
        {
            _session.PrologueExitLoaded = true;
            _session.ElizaIntroTriggered = true;
            _session.ElizaIntroSeen = true;
            _session.EasternRouteUnlocked = true;
            _session.SetCutsceneContext(SessionState.ElizaIntroCutsceneId,
                SessionState.ElizaPreContactCheckpointId);

            Assert.IsFalse(_session.ResumeInterruptedElizaIntro());
            Assert.IsTrue(_session.EasternRouteUnlocked);

            _session.ElizaIntroSeen = false;
            _session.SetCutsceneContext("OTHER_CUTSCENE", "other_checkpoint");
            Assert.IsFalse(_session.ResumeInterruptedElizaIntro());
            Assert.AreEqual("OTHER_CUTSCENE", _session.ActiveCutsceneId);
        }
    }
}
