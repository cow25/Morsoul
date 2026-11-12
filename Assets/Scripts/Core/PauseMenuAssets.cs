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
    }
}

