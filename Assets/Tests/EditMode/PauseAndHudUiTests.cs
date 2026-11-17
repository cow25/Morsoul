using System;
using Morsoul.Core;
using Morsoul.Hub;
using Morsoul.Title;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using UguiButton = UnityEngine.UI.Button;

namespace Morsoul.Tests
{
    public class PauseAndHudUiTests
    {
        float _savedTimeScale;

        [SetUp]
        public void SetUp()
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _savedTimeScale;
            if (PauseMenuController.Instance != null)
            {
                PauseMenuController.Instance.Close();
                UnityEngine.Object.DestroyImmediate(PauseMenuController.Instance.gameObject);
            }
            if (CoreRuntime.Instance != null)
            {
                UnityEngine.Object.DestroyImmediate(CoreRuntime.Instance.gameObject);
            }
        }

        [Test]
        public void PauseMenuConfig_ResourceExistsAndReferencesAssets()
        {
            var cfg = Resources.Load<PauseMenuAssets>("PauseMenuConfig");
            Assert.IsNotNull(cfg, "Resources/PauseMenuConfig.asset must exist.");
            Assert.IsNotNull(cfg.panelSettings, "PauseMenuConfig.panelSettings must be assigned.");
            Assert.IsNotNull(cfg.pauseMenuUxml, "PauseMenuConfig.pauseMenuUxml must be assigned.");
        }

        [Test]
        public void PauseMenuController_OpenCloseAndTimeScale()
        {
            var ctrl = PauseMenuController.EnsureRuntime();
            Assert.IsNotNull(ctrl);
            Assert.IsFalse(ctrl.IsOpen);
            Assert.IsFalse(PauseMenuController.IsPaused);

            ctrl.Open();
            Assert.IsTrue(ctrl.IsOpen);
            Assert.IsTrue(PauseMenuController.IsPaused);
            Assert.AreEqual(0f, Time.timeScale, 0.0001f);

            ctrl.Close();
            Assert.IsFalse(ctrl.IsOpen);
            Assert.IsFalse(PauseMenuController.IsPaused);
            Assert.AreEqual(1f, Time.timeScale, 0.0001f);
        }

        [Test]
        public void PauseMenuController_ExternalModalRegistration_AndUnregister()
        {
            bool modalOpen = true;
            Func<bool> pred = () => modalOpen;

            PauseMenuController.RegisterExternalModal(pred);
            try
            {
                Assert.IsTrue(pred());
                modalOpen = false;
                PauseMenuController.NotifyExternalModalClosed();
                Assert.IsFalse(pred());
            }
            finally
            {
                PauseMenuController.UnregisterExternalModal(pred);
            }
        }

        [Test]
        public void UnifiedHudAndOverlayUxml_ContainAllRequiredNamedElements()
        {
            AssertUxmlElements("Assets/Panels/3D/HUD.uxml",
                "HUD-Root", "UserNameLabel", "EscKeyButton", "FocusValueLabel", "FocusBarFill",
                "ObjectivePanel", "ActiveGoalTitle", "ObjectiveLabel", "CaseLabel",
                "GolemEyeRow", "AetherSightRow", "DeskKeyButton", "BoardKeyButton",
                "InteractionContainer", "InteractionText", "ItemDescriptionText");

            AssertUxmlElements("Assets/HubHud.uxml",
                "HubHud-Root", "UserNameLabel", "EscKeyButton", "FocusText", "FocusBarFill",
                "ObjectivePanel", "ActiveGoalTitle", "ObjectiveLabel", "CaseLabel",
                "GolemEyeRow", "AetherSightRow", "EmotionEchoRow", "DeskKeyButton", "BoardKeyButton",
                "FooterLabel");

            AssertUxmlElements("Assets/PauseMenu.uxml",
                "PauseMenu-Root", "DossierSurname", "DossierName", "DossierDob", "DossierCasesSolved",
                "DossierSex", "DossierSpecies", "ReputationLevelLabel", "ReputationBarFill", "ReputationPointsLabel",
                "DossierDateIssued", "DossierTitle", "PauseStatusBanner",
                "ActionsPage", "SaveStateButton", "SettingsButton", "QuitGameButton", "ResumeButton",
                "QuitConfirmRow", "ReturnTitleButton", "ExitAppButton", "QuitCancelButton",
                "SettingsPage", "MasterFill", "MusicFill", "SfxFill",
                "MasterValueLabel", "MusicValueLabel", "SfxValueLabel",
                "FullscreenToggleButton", "SettingsBackButton");

            AssertUxmlElements("Assets/Dialogue.uxml",
                "Dialogue-Root", "SpeakerPortrait", "OptionsContainer", "OptionButton0", "OptionButton1",
                "OptionButton2", "SpeakerLabel", "DialogueText", "ContinueHint",
                "NameInputContainer", "NameInputField", "NameInputConfirm", "FadeOverlay", "FadeText");

            AssertUxmlElements("Assets/Journal.uxml",
                "Journal-Root", "TabBody", "JournalTitle", "JournalType", "JournalDesc",
                "TabPhysical", "TabTestimony", "TabLogs", "TabDossier");

            AssertUxmlElements("Assets/Board.uxml",
                "Board-Root", "ClueRack", "PremiseRow", "FeedbackBanner", "PromptModal", "HintPopup",
                "HintPortrait", "CaseTitleLabel", "FeedbackLabel", "HintLabel", "PromptQuestion",
                "HintSpeaker", "HintText", "SlotA", "SlotB", "LinkButton", "SubmitButton",
                "CancelPromptButton", "HintClose", "CloseBoardButton",
                "OptionButton0", "OptionButton1", "OptionButton2");

            AssertUxmlElements("Assets/WeightedBoard.uxml",
                "WeightedBoard-Root", "NodeCanvas", "LinkList", "ConclusionList", "FeedbackBanner",
                "HintPopup", "HintPortrait", "CaseTitleLabel", "ErrorCountLabel", "FocusLabel",
                "LampLabel", "SupportLabel", "FeedbackLabel", "HintLabel", "HintSpeaker", "HintText",
                "SubmitButton", "HintClose", "CloseBoardButton");
        }

        [Test]
        public void TitleMenuController_SaveExistsTogglesContinueAndLoadButtons()
        {
            var root = new GameObject("TitleTest");
            try
            {
                var ctrl = root.AddComponent<TitleMenuController>();
                ctrl.continueButton = NewUguiButton("Continue", root.transform);
                ctrl.loadGameButton = ctrl.continueButton;
                ctrl.startGameButton = NewUguiButton("Start", root.transform);
                ctrl.newGameButton = NewUguiButton("New", root.transform);
                ctrl.quitButton = NewUguiButton("Quit", root.transform);

                ctrl.SetSaveExists(false);
                Assert.IsFalse(ctrl.continueButton.interactable);
                Assert.IsFalse(ctrl.loadGameButton.interactable);
                Assert.IsFalse(ctrl.startGameButton.gameObject.activeSelf);

                ctrl.SetSaveExists(true);
                Assert.IsTrue(ctrl.continueButton.interactable);
                Assert.IsTrue(ctrl.loadGameButton.interactable);
                Assert.IsTrue(ctrl.startGameButton.gameObject.activeSelf);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HubHudController_SetObjective_UpdatesAppliedObjectiveWithoutThrowing()
        {
            var go = new GameObject("HubHudTest");
            try
            {
                var hud = go.AddComponent<HubHudController>();
                hud.SetObjective("Talk to Guildmaster");
                Assert.AreEqual("Talk to Guildmaster", hud.AppliedObjective);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static void AssertUxmlElements(string assetPath, params string[] names)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);
            Assert.IsNotNull(tree, $"Missing VisualTreeAsset at {assetPath}");
            var cloned = tree.CloneTree();
            foreach (string elementName in names)
            {
                Assert.IsNotNull(cloned.Q<VisualElement>(elementName),
                    $"UXML '{assetPath}' is missing required element '{elementName}'.");
            }
        }

        [Test]
        public void HudUxml_AndPauseMenuConfig_ExposeJournalAndBoardOverlays()
        {
            AssertUxmlElements("Assets/Panels/3D/HUD.uxml",
                "DeskKeyButton", "BoardKeyButton", "EscKeyButton");

            var config = Resources.Load<PauseMenuAssets>("PauseMenuConfig");
            Assert.IsNotNull(config, "Resources/PauseMenuConfig.asset is missing.");
            Assert.IsNotNull(config.panelSettings);
            Assert.IsNotNull(config.pauseMenuUxml);
            Assert.IsNotNull(config.journalUxml);
            Assert.IsNotNull(config.weightedBoardUxml);
            Assert.IsNotNull(config.boardUxml);
            Assert.IsNotNull(config.interrogationUxml);
            Assert.IsNotNull(config.resolutionUxml);
        }

        static UguiButton NewUguiButton(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<UnityEngine.UI.Image>();
            var btn = go.AddComponent<UguiButton>();
            btn.targetGraphic = img;
            return btn;
        }
    }
}
