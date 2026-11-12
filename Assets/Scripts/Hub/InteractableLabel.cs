using Morsoul.Core;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Blockout world label drawn through IMGUI at the object's screen
    /// position. No font assets or render-pipeline support required, so it
    /// stays visible wherever the placeholder quads do. Replaceable by real
    /// sprite/TMP indicators when art lands.
    /// </summary>
    public class InteractableLabel : MonoBehaviour
    {
        public string text = "";
        public Color color = Color.white;
        public int fontSize = 16;

        static GUIStyle _style;

        void OnGUI()
        {
            if (string.IsNullOrEmpty(text)) return;
            if (PauseMenuController.AnyModalOrPauseOpen) return;
            var cam = Camera.main;
            if (cam == null) return;
            var screen = cam.WorldToScreenPoint(transform.position);
            if (screen.z <= 0f) return;

            _style ??= new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            _style.fontSize = fontSize;
            _style.normal.textColor = color;

            var size = _style.CalcSize(new GUIContent(text));
            var rect = new Rect(
                screen.x - size.x * 0.5f,
                Screen.height - screen.y - size.y * 0.5f,
                size.x, size.y);
            GUI.Label(rect, text, _style);
        }
    }
}
