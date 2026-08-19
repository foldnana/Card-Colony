using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft.NarrativeEditor
{
    public static class NarrativePresentationPrefabInstaller
    {
        private const string PresentationPath =
            "Assets/StackCraft/Prefabs/UI/NarrativePresentation.prefab";
        private const string UiRootPath =
            "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";
        private const string RoundedSpritePath =
            "Assets/UltimateCleanGUIPack/Common/Sprites/Shapes/Rounded/" +
            "Rounded - 200ppu.png";

        [MenuItem("Tools/StackCraft/Narrative/Install P0-B Presentation")]
        public static void Install()
        {
            CreatePresentationPrefab();
            AttachBootstrapToUiRoot();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Narrative] P0-B 独立表现预制体已安装。");
        }

        private static void CreatePresentationPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PresentationPath));
            var root = new GameObject(
                "NarrativePresentation",
                typeof(RectTransform));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                Stretch(rootRect);
                rootRect.SetAsFirstSibling();

                Image mapMask = CreateImage(
                    "MapMask", rootRect,
                    new Color(0.015f, 0.035f, 0.055f, 0.48f));
                mapMask.raycastTarget = false;

                RawImage fullscreen = CreateRawImage(
                    "FullscreenImage", rootRect, Color.white);
                fullscreen.raycastTarget = false;

                Image visualNovel = CreateImage(
                    "VisualNovelRoot", rootRect,
                    new Color(0.02f, 0.055f, 0.09f, 0.3f));
                visualNovel.raycastTarget = false;
                Image lowerFrame = CreateImage(
                    "VisualNovelDialogueBackdrop",
                    visualNovel.rectTransform,
                    new Color(0.025f, 0.075f, 0.115f, 0.94f));
                RectTransform frameRect = lowerFrame.rectTransform;
                frameRect.anchorMin = new Vector2(0.16f, 0.05f);
                frameRect.anchorMax = new Vector2(0.84f, 0.31f);
                frameRect.offsetMin = Vector2.zero;
                frameRect.offsetMax = Vector2.zero;
                lowerFrame.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    RoundedSpritePath);
                lowerFrame.type = Image.Type.Sliced;
                var outline = lowerFrame.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.15f, 0.62f, 0.9f, 0.8f);
                outline.effectDistance = new Vector2(2f, -2f);

                TMP_Text modeLabel = CreateText(
                    "ModeLabel", rootRect, "剧情", 24,
                    new Color(0.36f, 0.82f, 1f));
                RectTransform modeRect = modeLabel.rectTransform;
                modeRect.anchorMin = new Vector2(0.5f, 1f);
                modeRect.anchorMax = new Vector2(0.5f, 1f);
                modeRect.pivot = new Vector2(0.5f, 1f);
                modeRect.anchoredPosition = new Vector2(0f, -24f);
                modeRect.sizeDelta = new Vector2(240f, 42f);

                Button skipButton = CreateButton(
                    "SkipButton", rootRect, "跳过");
                RectTransform skipRect = skipButton.GetComponent<RectTransform>();
                skipRect.anchorMin = Vector2.one;
                skipRect.anchorMax = Vector2.one;
                skipRect.pivot = Vector2.one;
                skipRect.anchoredPosition = new Vector2(-26f, -26f);
                skipRect.sizeDelta = new Vector2(132f, 44f);

                NarrativePresentationView view =
                    root.AddComponent<NarrativePresentationView>();
                SerializedObject serialized = new SerializedObject(view);
                serialized.FindProperty("mapMask").objectReferenceValue =
                    mapMask.gameObject;
                serialized.FindProperty("visualNovelRoot").objectReferenceValue =
                    visualNovel.gameObject;
                serialized.FindProperty("fullscreenImage").objectReferenceValue =
                    fullscreen;
                serialized.FindProperty("skipButton").objectReferenceValue =
                    skipButton;
                serialized.FindProperty("modeLabel").objectReferenceValue =
                    modeLabel;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                mapMask.gameObject.SetActive(false);
                fullscreen.gameObject.SetActive(false);
                visualNovel.gameObject.SetActive(false);
                skipButton.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PresentationPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AttachBootstrapToUiRoot()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(UiRootPath);
            try
            {
                NarrativePresentationBootstrap bootstrap =
                    contents.GetComponent<NarrativePresentationBootstrap>() ??
                    contents.AddComponent<NarrativePresentationBootstrap>();
                NarrativePresentationView prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(PresentationPath)
                        ?.GetComponent<NarrativePresentationView>();
                SerializedObject serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("presentationPrefab")
                    .objectReferenceValue = prefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, UiRootPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            Stretch(image.rectTransform);
            return image;
        }

        private static RawImage CreateRawImage(
            string name,
            Transform parent,
            Color color)
        {
            var go = new GameObject(
                name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            RawImage image = go.GetComponent<RawImage>();
            image.color = color;
            Stretch(image.rectTransform);
            return image;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            string text,
            float size,
            Color color)
        {
            var go = new GameObject(
                name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string text)
        {
            Image image = CreateImage(
                name, parent, new Color(0.04f, 0.34f, 0.58f, 0.98f));
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                RoundedSpritePath);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.8f, 0.94f, 1f);
            colors.pressedColor = new Color(0.58f, 0.82f, 0.96f);
            button.colors = colors;
            TMP_Text label = CreateText(
                "Label", image.transform, text, 22f, Color.white);
            Stretch(label.rectTransform);
            return button;
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
