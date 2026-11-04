using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Morsoul.Title.Editor
{
    /// <summary>
    /// Builds the dedicated uGUI title scene: a dark camera, an EventSystem
    /// with the project's InputSystem_Actions, and a 1920x1080-scaled Canvas
    /// holding the MORSOUL title, detective-name field and the three menu
    /// buttons wired into <see cref="TitleMenuController"/>. Idempotent:
    /// an existing scene prompts an overwrite dialog.
    /// </summary>
    public static class TitleMenuSceneFactory
    {
        const string ScenePath = "Assets/Scenes/Title.unity";
        const string ActionsPath = "Assets/InputSystem_Actions.inputactions";

        static readonly Color BackgroundColor = new Color(0.04f, 0.03f, 0.055f);
        static readonly Color PanelColor = new Color(0.19f, 0.15f, 0.24f, 0.9f);
        static readonly Color AccentColor = new Color(0.51f, 0.31f, 0.63f);
        static readonly Color TextColor = Color.white;
        static readonly Color SubtitleColor = new Color(0.73f, 0.65f, 0.81f);

        [MenuItem("Morsoul/Scene/Create Title Scene")]
        static void CreateTitleScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null
                || File.Exists(ScenePath))
            {
                bool overwrite = EditorUtility.DisplayDialog("Create Title Scene",
                    $"'{ScenePath}' already exists. Overwrite it?", "Overwrite", "Cancel");
                if (!overwrite) return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas();
            var menu = CreateMenu(canvas.transform);

            if (EditorSceneManager.SaveScene(scene, ScenePath))
                Debug.Log($"TitleMenuSceneFactory: title scene saved to '{ScenePath}'. " +
                          "Add it to Build Settings as index 0.", menu);
            else
                Debug.LogError($"TitleMenuSceneFactory: failed to save '{ScenePath}'.");
        }

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
        }

        static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null)
            {
                module.actionsAsset =
                    AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
                if (module.actionsAsset == null)
                    Debug.LogWarning($"TitleMenuSceneFactory: no InputActionAsset at " +
                                     $"'{ActionsPath}'; assign one on the EventSystem.");
            }
        }

        static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static TitleMenuController CreateMenu(Transform parent)
        {
            var menu = NewUIRect("TitleMenu", parent, Vector2.zero, Vector2.zero);
            Stretch(menu.GetComponent<RectTransform>());

            var controller = menu.AddComponent<TitleMenuController>();

            var title = NewText("Title", menu.transform, "MORSOUL", 96,
                FontStyles.Bold, TextColor);
            Place(title.GetComponent<RectTransform>(), new Vector2(0.5f, 0.68f),
                new Vector2(900f, 140f));

            var subtitle = NewText("Subtitle", menu.transform, "World's Case", 30,
                FontStyles.Normal, SubtitleColor);
            Place(subtitle.GetComponent<RectTransform>(), new Vector2(0.5f, 0.6f),
                new Vector2(600f, 50f));

            var nameField = CreateInputField("NameField", menu.transform,
                new Vector2(0.5f, 0.47f));

            var newGame = CreateButton("NewGameButton", menu.transform,
                "New Game", new Vector2(0.5f, 0.37f));
            var cont = CreateButton("ContinueButton", menu.transform,
                "Continue", new Vector2(0.5f, 0.3f));
            var quit = CreateButton("QuitButton", menu.transform,
                "Quit", new Vector2(0.5f, 0.23f));

            controller.nameField = nameField;
            controller.newGameButton = newGame;
            controller.continueButton = cont;
            controller.quitButton = quit;
            return controller;
        }

        static TMP_InputField CreateInputField(string name, Transform parent, Vector2 anchor)
        {
            var go = NewUIRect(name, parent, anchor, new Vector2(420f, 56f));
            var image = go.AddComponent<Image>();
            image.color = new Color(0.12f, 0.10f, 0.15f, 0.9f);

            var input = go.AddComponent<TMP_InputField>();

            var textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(go.transform, false);
            var areaRect = textArea.GetComponent<RectTransform>();
            Stretch(areaRect);
            areaRect.offsetMin = new Vector2(12f, 6f);
            areaRect.offsetMax = new Vector2(-12f, -6f);
            var mask = textArea.AddComponent<RectMask2D>();

            var text = NewText("Text", textArea.transform, "", 24,
                FontStyles.Normal, TextColor);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var textRect = text.GetComponent<RectTransform>();
            Stretch(textRect);

            var placeholder = NewText("Placeholder", textArea.transform,
                "Detective name...", 24, FontStyles.Italic,
                new Color(1f, 1f, 1f, 0.4f));
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(placeholder.GetComponent<RectTransform>());

            input.textViewport = areaRect;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.pointSize = 24;
            return input;
        }

        static Button CreateButton(string name, Transform parent, string label, Vector2 anchor)
        {
            var go = NewUIRect(name, parent, anchor, new Vector2(420f, 60f));
            var image = go.AddComponent<Image>();
            image.color = PanelColor;
            var button = go.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = PanelColor;
            colors.highlightedColor = new Color(0.28f, 0.22f, 0.37f);
            colors.pressedColor = new Color(0.35f, 0.28f, 0.45f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.12f, 0.10f, 0.15f, 0.6f);
            button.colors = colors;
            button.targetGraphic = image;

            var text = NewText("Label", go.transform, label, 26,
                FontStyles.Normal, TextColor);
            Stretch(text.GetComponent<RectTransform>());
            return button;
        }

        // --- UI helpers -------------------------------------------------------

        static GameObject NewUIRect(string name, Transform parent, Vector2 anchor, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Place(rect, anchor, size);
            return go;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static TextMeshProUGUI NewText(string name, Transform parent, string content,
            float size, FontStyles style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }
    }
}
