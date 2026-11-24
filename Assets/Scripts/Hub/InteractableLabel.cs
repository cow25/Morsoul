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
        Camera _cam;
        bool _isEmote;
        bool _isNameLabel;
        bool _significantOwner;
        GUIContent _cachedContent;
        string _cachedText;

        void Awake()
        {
            _isEmote = gameObject.name.IndexOf("Emote", StringComparison.OrdinalIgnoreCase) >= 0;
            _isNameLabel = gameObject.name == "NameLabel";
            _significantOwner = !_isNameLabel || IsSignificantOwner();
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(text)) return;
            if (PauseMenuController.AnyModalOrPauseOpen) return;
            if (!_isEmote)
            {
                if (_context == null) _context = FindAnyObjectByType<HubContext>();
                if (_context != null && _context.UiBlocked) return;
                if (_isNameLabel && !_significantOwner) return;
            }
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            var screen = _cam.WorldToScreenPoint(transform.position);
            if (screen.z <= 0f) return;

            _style ??= new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            _style.fontSize = fontSize;
            _style.normal.textColor = color;

            if (_cachedContent == null || _cachedText != text)
            {
                _cachedText = text;
                _cachedContent = new GUIContent(text);
            }
            var size = _style.CalcSize(_cachedContent);
            var rect = new Rect(
                screen.x - size.x * 0.5f,
                Screen.height - screen.y - size.y * 0.5f,
                size.x, size.y);
            GUI.Label(rect, _cachedContent, _style);
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
