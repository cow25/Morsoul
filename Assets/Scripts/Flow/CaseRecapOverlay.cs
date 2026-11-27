using System;
using Morsoul.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Morsoul.Flow
{
    /// <summary>
    /// Lightweight runtime modal showing a brief narrative recap between cases.
    /// </summary>
    public class CaseRecapOverlay : MonoBehaviour
    {
        static CaseRecapOverlay _active;
        Action _onDismissed;
        float _shownAt;

        public static void Show(int caseId, string gradeLabel, bool uneaseActive, Action onDismissed)
        {
            if (_active != null) Destroy(_active.gameObject);
            var go = new GameObject("CaseRecapOverlay");
            DontDestroyOnLoad(go);
            _active = go.AddComponent<CaseRecapOverlay>();
            _active.Build(caseId, gradeLabel, uneaseActive, onDismissed);
        }

        void Build(int caseId, string gradeLabel, bool uneaseActive, Action onDismissed)
        {
            _onDismissed = onDismissed;
            _shownAt = Time.unscaledTime;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 185;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            gameObject.AddComponent<GraphicRaycaster>();

            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(transform, false);
            var bImg = backdrop.AddComponent<Image>();
            bImg.color = new Color(0.05f, 0.04f, 0.07f, 0.88f);
            var bRt = backdrop.GetComponent<RectTransform>();
            bRt.anchorMin = Vector2.zero;
            bRt.anchorMax = Vector2.one;
            bRt.offsetMin = Vector2.zero;
            bRt.offsetMax = Vector2.zero;

            var card = new GameObject("Card");
            card.transform.SetParent(backdrop.transform, false);
            var cImg = card.AddComponent<Image>();
            cImg.color = new Color(0.14f, 0.11f, 0.09f, 0.98f);
            var cRt = card.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0.5f, 0.5f);
            cRt.anchorMax = new Vector2(0.5f, 0.5f);
            cRt.sizeDelta = new Vector2(640f, 320f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GetSummary(caseId, out string title, out string summary);
            string gradeLine = $"Verdict Grade: {gradeLabel}" + (uneaseActive ? "   •   An uneasy trace still lingers..." : "");

            AddText(card.transform, font, title, 24, FontStyle.Bold, new Color(0.96f, 0.84f, 0.52f), new Vector2(0, 108), new Vector2(580, 40));
            AddText(card.transform, font, gradeLine, 15, FontStyle.Bold, new Color(0.82f, 0.78f, 0.70f), new Vector2(0, 68), new Vector2(580, 28));
            AddText(card.transform, font, summary, 17, FontStyle.Normal, new Color(0.95f, 0.92f, 0.86f), new Vector2(0, -4), new Vector2(560, 130));

            var btnGo = new GameObject("ContinueButton");
            btnGo.transform.SetParent(card.transform, false);
            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.32f, 0.24f, 0.16f, 1f);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchoredPosition = new Vector2(0, -112);
            btnRt.sizeDelta = new Vector2(210, 40);
            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(Dismiss);
            AddText(btnGo.transform, font, "Continue  [E / Enter]", 15, FontStyle.Bold, new Color(0.98f, 0.93f, 0.80f), Vector2.zero, new Vector2(200, 36));
        }

        void Update()
        {
            if (Time.unscaledTime - _shownAt < 0.2f) return;
            var kb = Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                Dismiss();
        }

        void Dismiss()
        {
            var cb = _onDismissed;
            _onDismissed = null;
            if (_active == this) _active = null;
            Destroy(gameObject);
            cb?.Invoke();
        }

        static void AddText(Transform parent, Font font, string content, int size, FontStyle style, Color color, Vector2 pos, Vector2 box)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
        }

        static void GetSummary(int caseId, out string title, out string summary)
        {
            switch (caseId)
            {
                case 0:
                    title = "Prologue Recap — Awakening in the Ruins";
                    summary = "Your Golem Eye sparked back to life beside Eliza in the ruins. Together you pieced together the broken conduit and reached the Guild.";
                    break;
                case 1:
                    title = "Case 1 Recap — The Poisoned Vault";
                    summary = "By tracing the vault's tampered seal and residue, you exposed the culprit behind the poisoning and restored order to the Lower Market.";
                    break;
                case 2:
                    title = "Case 2 Recap — The Cathedral Incident";
                    summary = "Inside the Cathedral grounds, fractured testimonies and hidden marks revealed how the relic was compromised.";
                    break;
                case 3:
                    title = "Case 3 Recap — Whisperwood Trail";
                    summary = "Along the misted Whisperwood boundary, displaced tracks and resonance echoes uncovered what passed through the woods.";
                    break;
                case 4:
                    title = "Case 4 Recap — Iron Hollow Railhead";
                    summary = "At the Iron Hollow railhead, industrial logs and forged manifests pointed straight toward the final conspiracy.";
                    break;
                default:
                    title = $"Case {caseId} Recap";
                    summary = "The investigation is closed and your findings are recorded in the Guild ledger.";
                    break;
            }
        }
    }
}
