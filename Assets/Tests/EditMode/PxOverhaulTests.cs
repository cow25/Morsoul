using System;
using System.Collections.Generic;
using Morsoul.Core;
using Morsoul.Data;
using Morsoul.Hub;
using Morsoul.Interrogation;
using Morsoul.Journal;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Morsoul.Tests
{
    public class PxOverhaulTests
    {
        bool _savedAutoSuppress;

        [SetUp]
        public void SetUp()
        {
            _savedAutoSuppress = TutorialCatalog.AutoTutorialsSuppressed;
            TutorialCatalog.AutoTutorialsSuppressed = false;
        }

        [TearDown]
        public void TearDown()
        {
            TutorialCatalog.AutoTutorialsSuppressed = _savedAutoSuppress;
            if (TutorialOverlayController.Instance != null)
                UnityEngine.Object.DestroyImmediate(TutorialOverlayController.Instance.gameObject);
            if (PauseMenuController.Instance != null)
                UnityEngine.Object.DestroyImmediate(PauseMenuController.Instance.gameObject);
            if (CoreRuntime.Instance != null)
                UnityEngine.Object.DestroyImmediate(CoreRuntime.Instance.gameObject);
        }

        [Test]
        public void TutorialCatalog_UniqueIds_NonEmptyFields_AndNoInterrogationRedesignTutorial()
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in TutorialCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Id));
                Assert.IsTrue(seenIds.Add(entry.Id), $"Duplicate tutorial id '{entry.Id}'.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Title));
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Keybinds));
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Summary));
                Assert.IsNotNull(entry.Steps);
                Assert.Greater(entry.Steps.Length, 0);
            }

            Assert.IsNotNull(TutorialCatalog.Find("TUT_INTERROGATION"));

            Assert.IsNotNull(TutorialCatalog.Find("TUT_MOVE_3D"));
            Assert.IsNotNull(TutorialCatalog.Find("TUT_MOVE_2D"));
            Assert.IsNotNull(TutorialCatalog.Find("TUT_MOUSE_3D"));
            Assert.IsNotNull(TutorialCatalog.Find("TUT_BOARD_BASICS"));
            Assert.IsNotNull(TutorialCatalog.Find("TUT_REST_SPOT"));
        }

        [Test]
        public void TutorialCatalog_MarkSeen_PersistsAcrossSessionSaveAndRestore()
        {
            var session = new Morsoul.Core.SessionState();
            Assert.IsFalse(TutorialCatalog.IsSeen(session, "TUT_BOARD_BASICS"));
            TutorialCatalog.MarkSeen(session, "TUT_BOARD_BASICS");
            Assert.IsTrue(TutorialCatalog.IsSeen(session, "TUT_BOARD_BASICS"));

            var save = new SaveState();
            session.ApplyTo(save);
            var restored = new Morsoul.Core.SessionState();
            restored.RestoreFrom(save);
            Assert.IsTrue(TutorialCatalog.IsSeen(restored, "TUT_BOARD_BASICS"));
        }

        [Test]
        public void ClueHighlightFormatter_NeutralizesRedHerring_SubstitutesPlayerName_AndFormatsBold()
        {
            var rh = ScriptableObject.CreateInstance<ClueData>();
            rh.id = "RH1_01";
            rh.type = ClueType.RedHerring;
            rh.description = "Cheap <b>warped wrench</b>; test {PlayerName}.";

            try
            {
                string cat = ClueHighlightFormatter.FriendlyCategory(rh);
                Assert.AreEqual("Observation", cat);
                StringAssert.DoesNotContain("RedHerring", cat);

                string formatted = ClueHighlightFormatter.FormatDescription(rh, "Rook", darkBackground: false);
                StringAssert.Contains($"<color={ClueHighlightFormatter.LightAccentHex}><b>warped wrench</b></color>", formatted);
                StringAssert.Contains("Rook", formatted);
                StringAssert.DoesNotContain("{PlayerName}", formatted);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rh);
            }
        }

        [Test]
        public void FocusModel_AetherSight_DrainsPerTickWithoutStrainedLatch()
        {
            var focus = new FocusModel();
            focus.TickAetherSight(0.5f); // five 0.1s ticks at 0.3 each = 1.5
            Assert.AreEqual(100f - 5f * FocusTuning.AetherSightDrainPerTick, focus.Percent, 0.001f);
            Assert.IsFalse(focus.StrainedScanUsed);
        }

        [Test]
        public void RestSpot_EnforcesPerCaseBudgetAndCooldownAndGainCap()
        {
            var core = CoreRuntime.Ensure();
            core.Session.Reset();
            core.Session.CurrentCase = 1;
            core.Focus.Restore(10f, false, 100f);

            var go = new GameObject("BenchTest");
            var rest = go.AddComponent<RestSpot>();
            try
            {
                Assert.AreEqual(FocusTuning.RestMaxPerCase, core.Session.GetRemainingRests(1));

                rest.StartResting();
                Assert.IsTrue(rest.IsResting);
                Assert.AreEqual(FocusTuning.RestMaxPerCase - 1, core.Session.GetRemainingRests(1));
                rest.StopResting();

                // Second attempt inside cooldown window must be blocked
                float cd = core.Session.RestCooldownRemaining(Time.unscaledTime);
                Assert.Greater(cd, 0f);
                rest.StartResting();
                Assert.IsFalse(rest.IsResting);
                Assert.AreEqual(FocusTuning.RestMaxPerCase - 1, core.Session.GetRemainingRests(1));

                // Exhaust remaining rest slot via session record
                core.Session.RecordRestUse(1, -999f);
                Assert.AreEqual(0, core.Session.GetRemainingRests(1));
                rest.StartResting();
                Assert.IsFalse(rest.IsResting);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void InspectBoundsHelper_ComputesBoundsAcrossCompoundRenderers()
        {
            var root = new GameObject("InspectRootTest");
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(2f, 5f, -3f);
            child.transform.localScale = new Vector3(2f, 4f, 2f);
            try
            {
                Assert.IsTrue(InspectBoundsUtility.TryComputeRendererBounds(root, out var bounds));
                Assert.AreEqual(2f, bounds.center.x, 0.01f);
                Assert.AreEqual(5f, bounds.center.y, 0.01f);
                Assert.AreEqual(4f, bounds.size.y, 0.01f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void JournalController_OpenAllowed_RespectsExternalModalBlockers()
        {
            var go = new GameObject("JournalGuardTest");
            var journal = go.AddComponent<JournalController>();
            bool modalOpen = true;
            journal.CanOpen = () => !modalOpen;
            try
            {
                Assert.IsFalse(journal.OpenAllowed());
                modalOpen = false;
                Assert.IsTrue(journal.OpenAllowed());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void InterrogationEngine_LockedDoesNotLeakRebuttal_AndDetectsDifferentVerb()
        {
            var f = TestCases.Build(1, true);
            var engine = new InterrogationEngine(f.interrogation,
                new EvidenceData[] { f.c1, f.c2, f.t1, f.t2, f.rh1, f.p1, f.p2 });

            var locked = engine.Contradict(f.c1);
            Assert.AreEqual(MoveOutcome.Locked, locked.Outcome);
            Assert.AreNotEqual(f.interrogation.phases[0].validMoves[1].responseDialogue, locked.Response);

            Assert.IsTrue(engine.HasValidMoveWithDifferentVerb(f.c1, Verb.PresentEvidence, out var expected));
            Assert.AreEqual(Verb.Contradict, expected);
            Assert.IsFalse(engine.HasValidMoveWithDifferentVerb(f.rh1, Verb.PresentEvidence, out _));
        }

        [Test]
        public void InterrogationAssets_Cases2To4_HaveValidPressMovesAndMatchingEmotionEchoAuras()
        {
            for (int c = 2; c <= 4; c++)
            {
                var data = AssetDatabase.LoadAssetAtPath<InterrogationData>($"Assets/Data/Cases/Case0{c}/INT_0{c}.asset");
                Assert.IsNotNull(data, $"Missing INT_0{c}.asset");
                for (int p = 0; p < data.phases.Length; p++)
                {
                    var ph = data.phases[p];
                    var press = Array.Find(ph.validMoves, m => m != null && m.verb == Verb.Press);
                    Assert.IsNotNull(press, $"INT_0{c} phase {p} missing valid Verb.Press");

                    var win = Array.Find(ph.validMoves, m => m != null && m.breaksComposureSegment);
                    Assert.IsNotNull(win, $"INT_0{c} phase {p} missing winning move");

                    var expectedAura = win.verb == Verb.PresentEvidence ? Aura.Violet : Aura.Amber;
                    Assert.AreEqual(expectedAura, press.emotionAura, $"INT_0{c} phase {p} Emotion Echo aura mismatch");
                }
            }

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Interrogation.uxml");
            Assert.IsNotNull(uxml);
            var tree = uxml.CloneTree();
            Assert.IsNotNull(tree.Q<Button>("ContinueButton"));
            Assert.IsNotNull(tree.Q<VisualElement>("ForfeitConfirmRow"));
            Assert.IsNotNull(tree.Q<Button>("ForfeitConfirmButton"));
            Assert.IsNotNull(tree.Q<Button>("ForfeitCancelButton"));
        }
    }
}
