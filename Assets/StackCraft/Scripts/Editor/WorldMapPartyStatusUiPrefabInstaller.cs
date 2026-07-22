#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class WorldMapPartyStatusUiPrefabInstaller
    {
        private const string UiRootPath = "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";

        [MenuItem("Tools/StackCraft/Install World Map Party Status Panel")]
        public static void Install()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UiRootPath);
            try
            {
                Transform infoPanel = FindDescendant(root.transform, "InfoPanel");
                TMP_FontAsset font = FindDescendant(infoPanel, "InfoText")
                    .GetComponent<TMP_Text>().font;

                Transform oldPanel = FindDescendant(root.transform, "WorldMapPartyStatusPanel");
                if (oldPanel != null)
                    Object.DestroyImmediate(oldPanel.gameObject);

                GameObject panelObject = CreateUiObject(
                    "WorldMapPartyStatusPanel",
                    infoPanel.parent,
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup),
                    typeof(WorldMapPartyStatusView));
                panelObject.transform.SetSiblingIndex(infoPanel.GetSiblingIndex() + 1);

                RectTransform panelRect = (RectTransform)panelObject.transform;
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.zero;
                panelRect.pivot = Vector2.zero;
                panelRect.anchoredPosition = new Vector2(30f, 102f);
                panelRect.sizeDelta = new Vector2(420f, 340f);
                panelRect.localScale = Vector3.one;

                Image background = panelObject.GetComponent<Image>();
                background.color = new Color(0f, 0f, 0f, 0.9f);
                background.raycastTarget = false;

                CreateText(
                    "PanelTitle",
                    panelObject.transform,
                    font,
                    "小队",
                    32f,
                    new Color(0.22f, 0.72f, 0.95f),
                    TextAlignmentOptions.MidlineLeft,
                    new Vector2(0.06f, 0.82f),
                    new Vector2(0.94f, 0.96f));

                GameObject portraitObject = CreateUiObject(
                    "PartyPortrait",
                    panelObject.transform,
                    typeof(CanvasRenderer),
                    typeof(RawImage));
                SetRect(
                    (RectTransform)portraitObject.transform,
                    new Vector2(0.06f, 0.43f),
                    new Vector2(0.34f, 0.79f));
                RawImage portrait = portraitObject.GetComponent<RawImage>();
                portrait.raycastTarget = false;
                portrait.enabled = false;

                TMP_Text partyName = CreateText(
                    "PartyName",
                    panelObject.transform,
                    font,
                    "旅行小队",
                    26f,
                    Color.white,
                    TextAlignmentOptions.MidlineLeft,
                    new Vector2(0.39f, 0.67f),
                    new Vector2(0.94f, 0.79f));
                TMP_Text health = CreateText(
                    "PartyHealthText",
                    panelObject.transform,
                    font,
                    "生命  15/15",
                    24f,
                    Color.white,
                    TextAlignmentOptions.MidlineLeft,
                    new Vector2(0.39f, 0.56f),
                    new Vector2(0.94f, 0.67f));

                Image healthBackground = CreateImage(
                    "PartyHealthBar",
                    panelObject.transform,
                    new Color(0.2f, 0.2f, 0.2f, 1f),
                    new Vector2(0.39f, 0.51f),
                    new Vector2(0.94f, 0.55f));
                Image healthFill = CreateImage(
                    "PartyHealthFill",
                    healthBackground.transform,
                    new Color(0.24f, 0.72f, 0.38f, 1f),
                    Vector2.zero,
                    Vector2.one);
                healthFill.type = Image.Type.Filled;
                healthFill.fillMethod = Image.FillMethod.Horizontal;
                healthFill.fillOrigin = 0;
                healthFill.fillAmount = 1f;

                CreateImage(
                    "PartyDivider",
                    panelObject.transform,
                    new Color(1f, 1f, 1f, 0.16f),
                    new Vector2(0.06f, 0.38f),
                    new Vector2(0.94f, 0.385f));

                TMP_Text location = CreateText(
                    "PartyLocationText",
                    panelObject.transform,
                    font,
                    "所在地点：未知",
                    23f,
                    new Color(0.9f, 0.9f, 0.9f),
                    TextAlignmentOptions.MidlineLeft,
                    new Vector2(0.07f, 0.27f),
                    new Vector2(0.93f, 0.37f));
                TMP_Text members = CreateText(
                    "PartyMembersText",
                    panelObject.transform,
                    font,
                    "成员：1",
                    23f,
                    new Color(0.9f, 0.9f, 0.9f),
                    TextAlignmentOptions.MidlineLeft,
                    new Vector2(0.07f, 0.17f),
                    new Vector2(0.93f, 0.27f));
                TMP_Text state = CreateText(
                    "PartyStateText",
                    panelObject.transform,
                    font,
                    "状态：驻扎中",
                    23f,
                    new Color(0.95f, 0.78f, 0.24f),
                    TextAlignmentOptions.MidlineLeft,
                    new Vector2(0.07f, 0.07f),
                    new Vector2(0.93f, 0.17f));

                var serializedView = new SerializedObject(
                    panelObject.GetComponent<WorldMapPartyStatusView>());
                SetReference(serializedView, "portraitImage", portrait);
                SetReference(serializedView, "partyNameLabel", partyName);
                SetReference(serializedView, "healthLabel", health);
                SetReference(serializedView, "healthFill", healthFill);
                SetReference(serializedView, "locationLabel", location);
                SetReference(serializedView, "membersLabel", members);
                SetReference(serializedView, "stateLabel", state);
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;

                PrefabUtility.SaveAsPrefabAsset(root, UiRootPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Installed the fixed world-map party status panel into UIRoot.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject CreateUiObject(
            string name,
            Transform parent,
            params System.Type[] components)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            gameObject.transform.SetParent(parent, false);
            foreach (System.Type component in components)
                gameObject.AddComponent(component);
            return gameObject;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string text,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject gameObject = CreateUiObject(
                name,
                parent,
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            SetRect((RectTransform)gameObject.transform, anchorMin, anchorMax);
            TextMeshProUGUI label = gameObject.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject gameObject = CreateUiObject(
                name,
                parent,
                typeof(CanvasRenderer),
                typeof(Image));
            SetRect((RectTransform)gameObject.transform, anchorMin, anchorMax);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void SetReference(SerializedObject serialized, string name, Object value)
        {
            serialized.FindProperty(name).objectReferenceValue = value;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name)
                    return child;

                Transform nested = FindDescendant(child, name);
                if (nested != null)
                    return nested;
            }

            return null;
        }
    }
}
#endif
