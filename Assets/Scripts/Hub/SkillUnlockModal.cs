using System.Collections;
using Morsoul.Core;
using Morsoul.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Morsoul.Hub
{
    /// <summary>
    /// Full-screen achievement/unlock modal shown when a dormant chassis skill
    /// (such as Emotion Echo after Case 1) awakens. Displays the glowing skill
    /// icon, lore origin, and gameplay usage until dismissed with [E]/click.
    /// </summary>
    public class SkillUnlockModal : MonoBehaviour
    {
        public static IEnumerator Show(Tool tool, HubContext context = null)
        {
            var config = Resources.Load<PauseMenuAssets>("PauseMenuConfig");
            if (config == null || config.panelSettings == null) yield break;

            var go = new GameObject("SkillUnlockModal");
            var pr = go.AddComponent<PanelRenderer>();
            pr.panelSettings = config.panelSettings;
            pr.visualTreeAsset = config.pauseMenuUxml;
            pr.sortingOrder = 95;

            var modal = go.AddComponent<SkillUnlockModal>();
            yield return modal.Run(pr, tool, context);
            Destroy(go);
        }

        IEnumerator Run(PanelRenderer pr, Tool tool, HubContext context)
        {
            bool dismissed = false;
            System.Func<bool> blocker = () => !dismissed;
            context?.AddUiBlocker(blocker);
            PauseMenuController.RegisterExternalModal(blocker);

            var prevLock = UnityEngine.Cursor.lockState;
            var prevVisible = UnityEngine.Cursor.visible;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            VisualElement panelRoot = null;
            pr.RegisterUIReloadCallback((_, uiRoot, __) => panelRoot = uiRoot);
            float wait = 0f;
            while (panelRoot == null && wait < 1f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            if (panelRoot == null)
            {
                context?.RemoveUiBlocker(blocker);
                PauseMenuController.UnregisterExternalModal(blocker);
                UnityEngine.Cursor.lockState = prevLock;
                UnityEngine.Cursor.visible = prevVisible;
                yield break;
            }
            var root = new VisualElement();
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.right = 0;
            root.style.bottom = 0;
            root.style.backgroundColor = new Color(0.03f, 0.02f, 0.05f, 0.84f);
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;

            var bodyFont = Resources.Load<Font>("EBGaramond"); // optional fallback; USS/Assets loaded below
#if UNITY_EDITOR
            if (bodyFont == null)
                bodyFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/EBGaramond.ttf");
            var titleFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Middleearth.ttf");
            string iconPath = tool == Tool.AetherSight
                ? "Assets/Art/UI/Ability_AetherSight.png"
                : "Assets/Art/UI/Ability_EmotionEcho.png";
            var iconTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
#else
            Font titleFont = null;
            Texture2D iconTex = null;
#endif
            if (bodyFont != null) root.style.unityFont = bodyFont;

            var card = new VisualElement();
            card.style.width = 560;
            card.style.maxWidth = Length.Percent(90);
            card.style.backgroundColor = new Color(0.08f, 0.06f, 0.11f, 0.97f);
            card.style.borderTopLeftRadius = 10;
            card.style.borderTopRightRadius = 10;
            card.style.borderBottomLeftRadius = 10;
            card.style.borderBottomRightRadius = 10;
            card.style.borderTopWidth = 2;
            card.style.borderRightWidth = 2;
            card.style.borderBottomWidth = 2;
            card.style.borderLeftWidth = 2;
            Color accent = tool == Tool.AetherSight
                ? new Color(0.28f, 0.85f, 0.95f, 1f)
                : new Color(0.76f, 0.48f, 0.95f, 1f);
            SetBorderColor(card, accent);
            card.style.paddingTop = 26;
            card.style.paddingBottom = 24;
            card.style.paddingLeft = 32;
            card.style.paddingRight = 32;
            card.style.alignItems = Align.Center;

            var badge = new Label("NEW SKILL UNLOCKED");
            badge.style.fontSize = 14;
            badge.style.color = new Color(0.92f, 0.78f, 0.42f, 1f);
            badge.style.unityFontStyleAndWeight = FontStyle.Bold;
            badge.style.marginBottom = 14;
            card.Add(badge);

            var glowFrame = new VisualElement();
            glowFrame.style.width = 96;
            glowFrame.style.height = 96;
            glowFrame.style.paddingTop = 8;
            glowFrame.style.paddingBottom = 8;
            glowFrame.style.paddingLeft = 8;
            glowFrame.style.paddingRight = 8;
            glowFrame.style.borderTopLeftRadius = 14;
            glowFrame.style.borderTopRightRadius = 14;
            glowFrame.style.borderBottomLeftRadius = 14;
            glowFrame.style.borderBottomRightRadius = 14;
            glowFrame.style.borderTopWidth = 3;
            glowFrame.style.borderRightWidth = 3;
            glowFrame.style.borderBottomWidth = 3;
            glowFrame.style.borderLeftWidth = 3;
            glowFrame.style.backgroundColor = new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 0.65f);
            SetBorderColor(glowFrame, accent);
            glowFrame.style.marginBottom = 14;

            var iconEl = new VisualElement();
            iconEl.style.width = Length.Percent(100);
            iconEl.style.height = Length.Percent(100);
            if (iconTex != null)
                iconEl.style.backgroundImage = new StyleBackground(iconTex);
            glowFrame.Add(iconEl);
            card.Add(glowFrame);

            string skillName = tool == Tool.AetherSight ? "Aether Sight" : "Emotion Echo";
            var title = new Label(skillName);
            if (titleFont != null) title.style.unityFont = titleFont;
            title.style.fontSize = 26;
            title.style.color = Color.white;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 10;
            card.Add(title);

            string loreText = tool == Tool.AetherSight
                ? "Ambient mana absorbed from the Whisperwood ritual site has recharged your chassis's dormant First-Age optical prism.\n\nIn 3D Investigations: Press [1] to toggle Aether Sight and reveal hidden runic inscriptions and mana conduits."
                : "A dormant First-Age module awakened through observing human behavior and your growing bond with Eliza.\n\nIn Interrogations: Press [1] on claims to read mana auras — Amber reveals deception ([2] Contradict), Violet reveals guarded motive ([3] Present Evidence).";

            var desc = new Label(loreText);
            desc.style.fontSize = 15;
            desc.style.color = new Color(0.92f, 0.90f, 0.86f, 1f);
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.unityTextAlign = TextAnchor.MiddleCenter;
            desc.style.marginBottom = 20;
            card.Add(desc);

            var continueBtn = new Button(() => dismissed = true) { text = "[E] Continue" };
            continueBtn.style.paddingTop = 8;
            continueBtn.style.paddingBottom = 8;
            continueBtn.style.paddingLeft = 24;
            continueBtn.style.paddingRight = 24;
            continueBtn.style.fontSize = 15;
            continueBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            continueBtn.style.backgroundColor = new Color(0.88f, 0.76f, 0.48f, 1f);
            continueBtn.style.color = new Color(0.12f, 0.08f, 0.05f, 1f);
            continueBtn.style.borderTopLeftRadius = 4;
            continueBtn.style.borderTopRightRadius = 4;
            continueBtn.style.borderBottomLeftRadius = 4;
            continueBtn.style.borderBottomRightRadius = 4;
            card.Add(continueBtn);

            root.Add(card);

            // Attach directly to the PanelRenderer's rootVisualElement if available.
            panelRoot.Clear();
            panelRoot.Add(root);

            float t = 0f;
            yield return null; // avoid consuming the same-frame E key that ended dialogue
            while (!dismissed)
            {
                t += Time.unscaledDeltaTime * 3.2f;
                float pulse = 0.5f + 0.5f * Mathf.Sin(t);
                var glowCol = Color.Lerp(accent * 0.7f, Color.white, pulse * 0.35f);
                glowCol.a = 1f;
                SetBorderColor(glowFrame, glowCol);
                glowFrame.style.backgroundColor = new Color(
                    accent.r * (0.25f + 0.25f * pulse),
                    accent.g * (0.25f + 0.25f * pulse),
                    accent.b * (0.25f + 0.25f * pulse),
                    0.75f);

                var kb = Keyboard.current;
                if (kb != null && (kb.eKey.wasPressedThisFrame ||
                                   kb.enterKey.wasPressedThisFrame ||
                                   kb.spaceKey.wasPressedThisFrame ||
                                   kb.escapeKey.wasPressedThisFrame))
                {
                    dismissed = true;
                }
                yield return null;
            }

            panelRoot.Remove(root);

            context?.RemoveUiBlocker(blocker);
            PauseMenuController.UnregisterExternalModal(blocker);
            PauseMenuController.NotifyExternalModalClosed();
            UnityEngine.Cursor.lockState = prevLock;
            UnityEngine.Cursor.visible = prevVisible;
        }

        static void SetBorderColor(VisualElement el, Color c)
        {
            el.style.borderTopColor = c;
            el.style.borderRightColor = c;
            el.style.borderBottomColor = c;
            el.style.borderLeftColor = c;
        }
    }
}
