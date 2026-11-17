using System;
using Morsoul.Core;
using UnityEngine;

namespace Morsoul.Hub
{
    /// <summary>
    /// Blockout world label drawn through IMGUI at the object's screen
    /// position. Hidden whenever any UI overlay/modal is open, and for
    /// floating NameLabels only shown on key characters/suspects.
    /// </summary>
    public class InteractableLabel : MonoBehaviour
    {
        public string text = "";
        public Color color = Color.white;
        public int fontSize = 16;

        static GUIStyle _style;
        HubContext _context;

        void OnGUI()
        {
            if (string.IsNullOrEmpty(text)) return;
            if (PauseMenuController.AnyModalOrPauseOpen) return;
            if (_context == null) _context = FindAnyObjectByType<HubContext>();
            if (_context != null && _context.UiBlocked) return;
            if (gameObject.name == "NameLabel" && !IsSignificantOwner()) return;
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

        bool IsSignificantOwner()
        {
            var owner = transform.parent;
            if (owner == null) return false;
            var npc = owner.GetComponent<NpcInteractable>();
            if (npc != null && npc.interrogationCaseId > 0) return true;
            string n = owner.name;
            return n.IndexOf("Eliza", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("Ovan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("Carrier", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("Kaelen", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("Thrun", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
