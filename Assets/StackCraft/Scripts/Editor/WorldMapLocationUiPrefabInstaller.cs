#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class WorldMapLocationUiPrefabInstaller
    {
        private const string UiRootPath = "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";

        [MenuItem("Tools/StackCraft/Install World Map Location Sidebar")]
        public static void Install()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UiRootPath);
            try
            {
                Transform menuPanel = FindDescendant(root.transform, "MenuPanel");
                Transform header = FindDescendant(menuPanel, "Header");
                Toggle questsToggle = FindDescendant(header, "QuestsToggle").GetComponent<Toggle>();
                Toggle recipesToggle = FindDescendant(header, "RecipesToggle").GetComponent<Toggle>();
                TMP_FontAsset font = questsToggle.GetComponentInChildren<TMP_Text>(true).font;

                Transform oldLocationToggle = FindDescendant(header, "LocationToggle");
                if (oldLocationToggle != null)
                    Object.DestroyImmediate(oldLocationToggle.gameObject);

                Transform oldLocationView = FindDescendant(menuPanel, "LocationView");
                if (oldLocationView != null)
                    Object.DestroyImmediate(oldLocationView.gameObject);

                GameObject locationToggleObject = Object.Instantiate(
                    questsToggle.gameObject,
                    header,
                    worldPositionStays: false);
                locationToggleObject.name = "LocationToggle";
                locationToggleObject.transform.SetSiblingIndex(0);
                Toggle locationToggle = locationToggleObject.GetComponent<Toggle>();
                locationToggle.onValueChanged = new Toggle.ToggleEvent();
                locationToggle.group = header.GetComponent<ToggleGroup>();
                locationToggle.SetIsOnWithoutNotify(false);
                locationToggleObject.GetComponentInChildren<TMP_Text>(true).text = "地点";

                GameObject locationViewObject = CreateUiObject(
                    "LocationView",
                    menuPanel,
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup),
                    typeof(WorldMapLocationView));
                RectTransform locationViewRect = (RectTransform)locationViewObject.transform;
                SetRect(
                    locationViewRect,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(0f, 0f),
                    new Vector2(0f, -60f));
                locationViewRect.anchoredPosition = new Vector2(0f, -30f);
                Image background = locationViewObject.GetComponent<Image>();
                background.color = new Color(0f, 0f, 0f, 0.9f);

                TMP_Text title = CreateText(
                    "LocationTitle",
                    locationViewObject.transform,
                    font,
                    "地点",
                    36f,
                    new Color(0.35f, 0.82f, 0.42f),
                    TextAlignmentOptions.Center,
                    new Vector2(0.06f, 0.88f),
                    new Vector2(0.94f, 0.97f));

                GameObject artObject = CreateUiObject(
                    "LocationArt",
                    locationViewObject.transform,
                    typeof(CanvasRenderer),
                    typeof(RawImage));
                SetRect(
                    (RectTransform)artObject.transform,
                    new Vector2(0.32f, 0.67f),
                    new Vector2(0.68f, 0.87f),
                    Vector2.zero,
                    Vector2.zero);
                RawImage art = artObject.GetComponent<RawImage>();
                art.raycastTarget = false;

                TMP_Text typeAndDanger = CreateText(
                    "LocationTypeAndDanger",
                    locationViewObject.transform,
                    font,
                    "地点 · 危险 1",
                    24f,
                    Color.white,
                    TextAlignmentOptions.Center,
                    new Vector2(0.06f, 0.61f),
                    new Vector2(0.94f, 0.67f));
                TMP_Text discovery = CreateText(
                    "LocationDiscovery",
                    locationViewObject.transform,
                    font,
                    "● 已发现",
                    23f,
                    new Color(0.38f, 0.75f, 0.36f),
                    TextAlignmentOptions.MidlineLeft,
                    new Vector2(0.07f, 0.55f),
                    new Vector2(0.93f, 0.61f));
                TMP_Text travelTime = CreateText(
                    "LocationTravelTime",
                    locationViewObject.transform,
                    font,
                    "旅行时间    1秒（临时）",
                    23f,
                    Color.white,
                    TextAlignmentOptions.MidlineLeft,
                    new Vector2(0.07f, 0.48f),
                    new Vector2(0.93f, 0.55f));
                TMP_Text resources = CreateText(
                    "LocationResources",
                    locationViewObject.transform,
                    font,
                    "可能资源\n• 未知",
                    22f,
                    new Color(0.9f, 0.9f, 0.9f),
                    TextAlignmentOptions.TopLeft,
                    new Vector2(0.07f, 0.31f),
                    new Vector2(0.93f, 0.48f));
                TMP_Text description = CreateText(
                    "LocationDescription",
                    locationViewObject.transform,
                    font,
                    "选择地点卡以查看详情。",
                    21f,
                    new Color(0.9f, 0.9f, 0.9f),
                    TextAlignmentOptions.TopLeft,
                    new Vector2(0.07f, 0.115f),
                    new Vector2(0.93f, 0.3f));
                description.enableWordWrapping = true;

                Button enterButton = CreateButton(
                    "EnterLocationButton",
                    locationViewObject.transform,
                    font,
                    "进入地点",
                    new Color(0.16f, 0.62f, 0.84f, 1f),
                    new Vector2(0.07f, 0.025f),
                    new Vector2(0.93f, 0.1f));
                enterButton.interactable = false;

                GameObject npcTradePanel = CreateUiObject(
                    "NpcTradePanel",
                    locationViewObject.transform,
                    typeof(CanvasRenderer),
                    typeof(Image));
                SetRect(
                    (RectTransform)npcTradePanel.transform,
                    new Vector2(0.04f, 0.11f),
                    new Vector2(0.96f, 0.61f),
                    Vector2.zero,
                    Vector2.zero);
                npcTradePanel.GetComponent<Image>().color =
                    new Color(0.035f, 0.045f, 0.055f, 0.98f);

                Button npcBuyTab = CreateButton(
                    "NpcBuyTabButton",
                    npcTradePanel.transform,
                    font,
                    "购买",
                    new Color(0.18f, 0.48f, 0.36f, 1f),
                    new Vector2(0.02f, 0.87f),
                    new Vector2(0.32f, 0.98f));
                Button npcSellTab = CreateButton(
                    "NpcSellTabButton",
                    npcTradePanel.transform,
                    font,
                    "出售",
                    new Color(0.48f, 0.34f, 0.16f, 1f),
                    new Vector2(0.35f, 0.87f),
                    new Vector2(0.65f, 0.98f));
                Button npcTalkTab = CreateButton(
                    "NpcTalkTabButton",
                    npcTradePanel.transform,
                    font,
                    "交谈",
                    new Color(0.25f, 0.38f, 0.55f, 1f),
                    new Vector2(0.68f, 0.87f),
                    new Vector2(0.98f, 0.98f));

                GameObject scrollObject = CreateUiObject(
                    "NpcTradeScrollView",
                    npcTradePanel.transform,
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(ScrollRect));
                SetRect(
                    (RectTransform)scrollObject.transform,
                    new Vector2(0.02f, 0.18f),
                    new Vector2(0.98f, 0.84f),
                    Vector2.zero,
                    Vector2.zero);
                scrollObject.GetComponent<Image>().color =
                    new Color(0f, 0f, 0f, 0.22f);

                GameObject viewport = CreateUiObject(
                    "Viewport",
                    scrollObject.transform,
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Mask));
                SetRect(
                    (RectTransform)viewport.transform,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero);
                viewport.GetComponent<Image>().color =
                    new Color(1f, 1f, 1f, 0.01f);
                viewport.GetComponent<Mask>().showMaskGraphic = false;

                GameObject contentObject = CreateUiObject(
                    "NpcTradeListContent",
                    viewport.transform,
                    typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter));
                RectTransform content = (RectTransform)contentObject.transform;
                SetRect(
                    content,
                    new Vector2(0f, 1f),
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero);
                content.pivot = new Vector2(0.5f, 1f);
                var layout = contentObject.GetComponent<VerticalLayoutGroup>();
                layout.spacing = 8f;
                layout.padding = new RectOffset(8, 8, 8, 8);
                layout.childControlWidth = true;
                layout.childForceExpandWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandHeight = false;
                contentObject.GetComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;

                ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
                scroll.viewport = (RectTransform)viewport.transform;
                scroll.content = content;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;

                NpcTradeListRowView rowTemplate = CreateNpcTradeRowTemplate(
                    contentObject.transform,
                    font);
                rowTemplate.gameObject.SetActive(false);
                TMP_Text npcTradeHint = CreateText(
                    "NpcTradeHint",
                    npcTradePanel.transform,
                    font,
                    "选择购买、出售或交谈。",
                    18f,
                    new Color(0.88f, 0.88f, 0.82f),
                    TextAlignmentOptions.TopLeft,
                    new Vector2(0.03f, 0.02f),
                    new Vector2(0.97f, 0.16f));
                npcTradeHint.enableWordWrapping = true;

                var serializedView = new SerializedObject(
                    locationViewObject.GetComponent<WorldMapLocationView>());
                SetReference(serializedView, "locationToggle", locationToggle);
                SetReference(serializedView, "questsToggle", questsToggle);
                SetReference(serializedView, "titleLabel", title);
                SetReference(serializedView, "artImage", art);
                SetReference(serializedView, "typeAndDangerLabel", typeAndDanger);
                SetReference(serializedView, "discoveryLabel", discovery);
                SetReference(serializedView, "travelTimeLabel", travelTime);
                SetReference(serializedView, "resourcesLabel", resources);
                SetReference(serializedView, "descriptionLabel", description);
                SetReference(serializedView, "enterLocationButton", enterButton);
                SetReference(serializedView, "npcTradePanel", npcTradePanel);
                SetReference(serializedView, "npcBuyTabButton", npcBuyTab);
                SetReference(serializedView, "npcSellTabButton", npcSellTab);
                SetReference(serializedView, "npcTalkTabButton", npcTalkTab);
                SetReference(serializedView, "npcTradeListRoot", content);
                SetReference(serializedView, "npcTradeRowTemplate", rowTemplate);
                SetReference(serializedView, "npcTradeHint", npcTradeHint);
                serializedView.ApplyModifiedPropertiesWithoutUndo();
                npcTradePanel.SetActive(false);

                CanvasGroup canvasGroup = locationViewObject.GetComponent<CanvasGroup>();
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                questsToggle.SetIsOnWithoutNotify(true);
                recipesToggle.SetIsOnWithoutNotify(false);

                PrefabUtility.SaveAsPrefabAsset(root, UiRootPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Installed world-map location sidebar into the native UIRoot prefab.");
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
            SetRect((RectTransform)gameObject.transform, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
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

        private static Button CreateButton(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string text,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float fontSize = 27f)
        {
            GameObject gameObject = CreateUiObject(
                name,
                parent,
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            SetRect((RectTransform)gameObject.transform, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            Button button = gameObject.GetComponent<Button>();
            button.targetGraphic = image;
            CreateText(
                "Label",
                gameObject.transform,
                font,
                text,
                fontSize,
                Color.white,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one);
            return button;
        }

        private static NpcTradeListRowView CreateNpcTradeRowTemplate(
            Transform parent,
            TMP_FontAsset font)
        {
            GameObject rowObject = CreateUiObject(
                "NpcTradeRowTemplate",
                parent,
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement),
                typeof(NpcTradeListRowView));
            rowObject.GetComponent<Image>().color =
                new Color(0.12f, 0.14f, 0.16f, 0.96f);
            LayoutElement rowLayout =
                rowObject.GetComponent<LayoutElement>();
            rowLayout.preferredHeight = 104f;
            rowLayout.flexibleWidth = 1f;

            GameObject iconObject = CreateUiObject(
                "Icon",
                rowObject.transform,
                typeof(CanvasRenderer),
                typeof(RawImage));
            SetRect(
                (RectTransform)iconObject.transform,
                new Vector2(0.02f, 0.12f),
                new Vector2(0.18f, 0.88f),
                Vector2.zero,
                Vector2.zero);
            RawImage icon = iconObject.GetComponent<RawImage>();
            icon.raycastTarget = false;

            TMP_Text details = CreateText(
                "Details",
                rowObject.transform,
                font,
                "商品\n价格 · 库存",
                18f,
                Color.white,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.2f, 0.08f),
                new Vector2(0.56f, 0.92f));
            details.enableWordWrapping = true;

            Button primary = CreateButton(
                "PrimaryButton",
                rowObject.transform,
                font,
                "购买",
                new Color(0.16f, 0.55f, 0.36f, 1f),
                new Vector2(0.58f, 0.18f),
                new Vector2(0.78f, 0.82f),
                18f);
            Button secondary = CreateButton(
                "SecondaryButton",
                rowObject.transform,
                font,
                "全部",
                new Color(0.46f, 0.34f, 0.16f, 1f),
                new Vector2(0.79f, 0.18f),
                new Vector2(0.99f, 0.82f),
                18f);

            var serializedRow = new SerializedObject(
                rowObject.GetComponent<NpcTradeListRowView>());
            SetReference(serializedRow, "icon", icon);
            SetReference(serializedRow, "detailsLabel", details);
            SetReference(serializedRow, "primaryButton", primary);
            SetReference(
                serializedRow,
                "primaryButtonLabel",
                primary.GetComponentInChildren<TMP_Text>(true));
            SetReference(serializedRow, "secondaryButton", secondary);
            SetReference(
                serializedRow,
                "secondaryButtonLabel",
                secondary.GetComponentInChildren<TMP_Text>(true));
            serializedRow.ApplyModifiedPropertiesWithoutUndo();
            return rowObject.GetComponent<NpcTradeListRowView>();
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void SetReference(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.objectReferenceValue = value;
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
