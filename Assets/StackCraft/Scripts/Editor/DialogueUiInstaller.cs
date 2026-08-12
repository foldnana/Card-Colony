#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class DialogueUiInstaller
    {
        private const string LocationScenePath = "Assets/StackCraft/Scenes/Location.unity";
        private const string PrefabPath = "Assets/StackCraft/Prefabs/UI/DialoguePanel.prefab";

        [MenuItem("Tools/Card Colony/Install Basic Dialogue UI")]
        public static void Install()
        {
            GameObject panelPrefab = BuildPanelPrefab();
            Scene locationScene = EditorSceneManager.OpenScene(
                LocationScenePath,
                OpenSceneMode.Additive);
            try
            {
                InstallIntoScene(locationScene, panelPrefab);
                EditorSceneManager.SaveScene(locationScene);
            }
            finally
            {
                EditorSceneManager.CloseScene(locationScene, true);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Card Colony/Rebuild Basic Dialogue Prefab")]
        public static void RebuildPrefab()
        {
            BuildPanelPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void InstallIntoScene(Scene locationScene, GameObject panelPrefab = null)
        {
            panelPrefab ??= BuildPanelPrefab();
            Canvas uiCanvas = locationScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .FirstOrDefault(canvas => canvas.name == "UICanvas");
            CombatManager combatManager = locationScene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CombatManager>(true))
                .FirstOrDefault();
            if (uiCanvas == null || combatManager == null)
                throw new System.InvalidOperationException(
                    "Location scene needs both UICanvas and CombatManager before installing dialogue UI.");

            Transform existing = uiCanvas.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == "DialoguePanel");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var panelObject = (GameObject)PrefabUtility.InstantiatePrefab(
                panelPrefab,
                locationScene);
            panelObject.transform.SetParent(uiCanvas.transform, false);
            panelObject.transform.SetAsLastSibling();

            DialogueManager manager = combatManager.GetComponent<DialogueManager>();
            if (manager == null)
                manager = combatManager.gameObject.AddComponent<DialogueManager>();
            var serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("dialoguePanel").objectReferenceValue =
                panelObject.GetComponent<DialoguePanelView>();
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(locationScene);
        }

        private static GameObject BuildPanelPrefab()
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset ?? AssetDatabase
                .FindAssets("t:TMP_FontAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>)
                .FirstOrDefault(asset => asset != null);

            var root = new GameObject(
                "DialoguePanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(CanvasGroup),
                typeof(DialoguePanelView));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 24f);
            rootRect.sizeDelta = new Vector2(720f, 220f);
            root.GetComponent<Image>().color = new Color(0.90f, 0.87f, 0.78f, 0.98f);
            Outline outline = root.GetComponent<Outline>();
            outline.effectColor = new Color(0.16f, 0.15f, 0.13f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            RawImage portraitBackground = CreateRawImage(
                "PortraitBackground",
                root.transform,
                new Vector2(20f, 20f),
                new Vector2(158f, 180f));
            portraitBackground.color = Color.white;
            portraitBackground.raycastTarget = false;

            RawImage portrait = CreateRawImage(
                "Portrait",
                root.transform,
                new Vector2(20f, 20f),
                new Vector2(158f, 180f));
            portrait.color = new Color(0.16f, 0.15f, 0.13f, 1f);
            portrait.raycastTarget = false;

            Image header = CreateImage(
                "SpeakerHeader",
                root.transform,
                new Vector2(188f, 166f),
                new Vector2(512f, 34f),
                new Color(0.68f, 0.48f, 0.17f, 1f));
            TMP_Text speakerName = CreateText(
                "SpeakerName",
                header.transform,
                font,
                22f,
                FontStyles.Bold,
                Color.white,
                TextAlignmentOptions.Center);
            Stretch(speakerName.rectTransform);

            TMP_Text dialogueText = CreateText(
                "DialogueText",
                root.transform,
                font,
                20f,
                FontStyles.Normal,
                new Color(0.16f, 0.15f, 0.13f, 1f),
                TextAlignmentOptions.TopLeft);
            SetBottomLeft(dialogueText.rectTransform, new Vector2(198f, 76f), new Vector2(492f, 80f));
            dialogueText.enableWordWrapping = true;

            var inlineActions = new GameObject(
                "InlineActions",
                typeof(RectTransform));
            inlineActions.transform.SetParent(root.transform, false);
            SetBottomLeft(
                inlineActions.GetComponent<RectTransform>(),
                new Vector2(188f, 0f),
                new Vector2(512f, 76f));

            Button replyButton = CreateButton(
                "ReplyButton",
                inlineActions.transform,
                font,
                "我该去哪里？",
                new Vector2(18f, 20f),
                new Vector2(268f, 48f),
                new Color(0.08f, 0.55f, 0.78f, 1f));
            Button goodbyeButton = CreateButton(
                "GoodbyeButton",
                inlineActions.transform,
                font,
                "告辞",
                new Vector2(306f, 20f),
                new Vector2(150f, 48f),
                new Color(0.42f, 0.42f, 0.40f, 1f));

            Image choiceTray = CreateImage(
                "DialogueChoiceTray",
                root.transform,
                new Vector2(188f, 236f),
                new Vector2(512f, 272f),
                new Color(0.67f, 0.72f, 0.77f, 0.99f));
            Outline trayOutline = choiceTray.gameObject.AddComponent<Outline>();
            trayOutline.effectColor = new Color(0.08f, 0.16f, 0.22f, 0.7f);
            trayOutline.effectDistance = new Vector2(2f, -2f);

            TMP_Text choiceTitle = CreateText(
                "ChoiceTitle",
                choiceTray.transform,
                font,
                21f,
                FontStyles.Bold,
                new Color(0.14f, 0.16f, 0.19f, 1f),
                TextAlignmentOptions.MidlineLeft);
            choiceTitle.text = "你的选择";
            SetBottomLeft(
                choiceTitle.rectTransform,
                new Vector2(20f, 224f),
                new Vector2(472f, 32f));

            var scrollObject = new GameObject(
                "ChoiceScrollView",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(ScrollRect));
            scrollObject.transform.SetParent(choiceTray.transform, false);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            SetBottomLeft(
                scrollRect,
                new Vector2(14f, 14f),
                new Vector2(484f, 204f));
            scrollObject.GetComponent<Image>().color =
                new Color(0.54f, 0.61f, 0.68f, 0.72f);

            var viewportObject = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Mask));
            viewportObject.transform.SetParent(scrollObject.transform, false);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            Stretch(viewport);
            viewport.offsetMin = new Vector2(8f, 8f);
            viewport.offsetMax = new Vector2(-8f, -8f);
            Image viewportImage = viewportObject.GetComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            var contentObject = new GameObject(
                "ChoiceContent",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportObject.transform, false);
            RectTransform choiceContent =
                contentObject.GetComponent<RectTransform>();
            choiceContent.anchorMin = new Vector2(0f, 1f);
            choiceContent.anchorMax = new Vector2(1f, 1f);
            choiceContent.pivot = new Vector2(0.5f, 1f);
            choiceContent.anchoredPosition = Vector2.zero;
            choiceContent.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout =
                contentObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(2, 2, 2, 2);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter =
                contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Button choiceButtonTemplate = CreateButton(
                "ChoiceButtonTemplate",
                contentObject.transform,
                font,
                "选择",
                Vector2.zero,
                new Vector2(0f, 48f),
                new Color(0.52f, 0.64f, 0.76f, 1f));
            LayoutElement choiceLayout =
                choiceButtonTemplate.gameObject.AddComponent<LayoutElement>();
            choiceLayout.minHeight = 48f;
            choiceLayout.preferredHeight = 48f;
            choiceButtonTemplate.gameObject.SetActive(false);

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = choiceContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            choiceTray.gameObject.SetActive(false);

            var view = root.GetComponent<DialoguePanelView>();
            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("portraitBackground").objectReferenceValue = portraitBackground;
            serializedView.FindProperty("portrait").objectReferenceValue = portrait;
            serializedView.FindProperty("speakerNameLabel").objectReferenceValue = speakerName;
            serializedView.FindProperty("dialogueTextLabel").objectReferenceValue = dialogueText;
            serializedView.FindProperty("replyButton").objectReferenceValue = replyButton;
            serializedView.FindProperty("replyButtonLabel").objectReferenceValue =
                replyButton.GetComponentInChildren<TMP_Text>(true);
            serializedView.FindProperty("goodbyeButton").objectReferenceValue = goodbyeButton;
            serializedView.FindProperty("inlineActions").objectReferenceValue = inlineActions;
            serializedView.FindProperty("choiceTray").objectReferenceValue = choiceTray.gameObject;
            serializedView.FindProperty("choiceTitleLabel").objectReferenceValue = choiceTitle;
            serializedView.FindProperty("choiceContent").objectReferenceValue = choiceContent;
            serializedView.FindProperty("choiceButtonTemplate").objectReferenceValue = choiceButtonTemplate;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            CommonFantasyHudSkinInstaller
                .ApplyToDialoguePrefabContents(root);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            SetBottomLeft(gameObject.GetComponent<RectTransform>(), position, size);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static RawImage CreateRawImage(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            gameObject.transform.SetParent(parent, false);
            SetBottomLeft(gameObject.GetComponent<RectTransform>(), position, size);
            return gameObject.GetComponent<RawImage>();
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            float fontSize,
            FontStyles style,
            Color color,
            TextAlignmentOptions alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            return text;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string label,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            Image image = CreateImage(name, parent, position, size, color);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            TMP_Text text = CreateText(
                "Label",
                button.transform,
                font,
                19f,
                FontStyles.Normal,
                Color.white,
                TextAlignmentOptions.Center);
            text.text = label;
            Stretch(text.rectTransform);
            return button;
        }

        private static void SetBottomLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
#endif
