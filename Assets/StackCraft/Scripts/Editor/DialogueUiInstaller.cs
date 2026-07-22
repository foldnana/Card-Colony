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

            Button replyButton = CreateButton(
                "ReplyButton",
                root.transform,
                font,
                "我该去哪里？",
                new Vector2(206f, 20f),
                new Vector2(268f, 48f),
                new Color(0.08f, 0.55f, 0.78f, 1f));
            Button goodbyeButton = CreateButton(
                "GoodbyeButton",
                root.transform,
                font,
                "告辞",
                new Vector2(494f, 20f),
                new Vector2(150f, 48f),
                new Color(0.42f, 0.42f, 0.40f, 1f));

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
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
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
