using UnityEngine;
using UnityEngine.UIElements;

namespace Morsoul.Core
{
    /// <summary>
    /// Resources-loaded bundle pointing to the shared Pause Menu UXML and PanelSettings
    /// so <see cref="PauseMenuController"/> can bootstrap itself cleanly in any 2D or 3D scene.
    /// </summary>
    [CreateAssetMenu(menuName = "Morsoul/UI/Pause Menu Assets", fileName = "PauseMenuConfig")]
    public class PauseMenuAssets : ScriptableObject
    {
        public PanelSettings panelSettings;
        public VisualTreeAsset pauseMenuUxml;

        [Header("Shared UI Overlay UXMLs (runtime HUD & Dev Preview)")]
        public VisualTreeAsset journalUxml;
        public VisualTreeAsset weightedBoardUxml;
        public VisualTreeAsset boardUxml;
        public VisualTreeAsset interrogationUxml;
        public VisualTreeAsset resolutionUxml;
        public VisualTreeAsset tutorialOverlayUxml;
        public VisualTreeAsset inspectUxml;
        public RenderTexture inspectRT;

        [Header("Runtime Fallback Assets (Standalone Builds)")]
        public ScriptableObject elizaProfile;
        public Texture2D carrierWagonTexture;
        public Texture2D horseTrotSheetTexture;
        public Texture2D carrierIdleSheetTexture;
        public Font bodyFont;
        public Font titleFont;
        public Texture2D abilityAetherSightIcon;
        public Texture2D abilityEmotionEchoIcon;

        [Header("Runtime SFX Bank")]
        public AudioClip sfxUiClick;
        public AudioClip sfxDialogueBlip;
        public AudioClip sfxClueCollected;
        public AudioClip sfxBoardLink;
        public AudioClip sfxBoardError;
        public AudioClip sfxBoardSolved;
        public AudioClip sfxInterrogationStrike;
        public AudioClip sfxInterrogationBreak;
        public AudioClip sfxScanPulse;
        public AudioClip sfxAetherToggle;
        public AudioClip sfxSkillUnlock;
        public AudioClip sfxCarriageLoop;
    }
}

