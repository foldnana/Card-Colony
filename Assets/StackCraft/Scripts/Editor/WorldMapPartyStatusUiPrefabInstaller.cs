#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class WorldMapPartyStatusUiPrefabInstaller
    {
        private const string UiRootPath =
            "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";
        private const string FillSpritePath =
            "Assets/UltimateCleanGUIPack/Common/Sprites/Shapes/" +
            "Semi Rounded/Semi Rounded - 300ppu.png";
        private const string OutlineSpritePath =
            "Assets/UltimateCleanGUIPack/Common/Sprites/Shapes/" +
            "Semi Rounded/Semi Rounded - Outline - 6px - 300ppu.png";

        [MenuItem("Tools/StackCraft/Install World Map Party Status Panel")]
        public static void Install()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UiRootPath);
            try
            {
                Transform infoPanel = FindDescendant(root.transform, "InfoPanel");
                TMP_FontAsset font = FindDescendant(infoPanel, "InfoText")
                    .GetComponent<TMP_Text>().font;
                Sprite fillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    FillSpritePath);
                Sprite outlineSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    OutlineSpritePath);

                Transform oldPanel = FindDescendant(
                    root.transform,
                    "WorldMapPartyStatusPanel");
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
                panelRect.anchorMin = new Vector2(0f, 0.5f);
                panelRect.anchorMax = new Vector2(0f, 0.5f);
                panelRect.pivot = new Vector2(0f, 0.5f);
                panelRect.anchoredPosition = new Vector2(18f, 48f);
                panelRect.sizeDelta = new Vector2(270f, 358f);

                Image background = panelObject.GetComponent<Image>();
                SetSlicedImage(
                    background,
                    fillSprite,
                    new Color(0.60f, 0.68f, 0.75f, 0.985f));

                TMP_Text title = CreateText(
                    "PanelTitle",
                    panelObject.transform,
                    font,
                    "当前小队",
                    21f,
                    new Color(0.05f, 0.24f, 0.31f),
                    TextAlignmentOptions.MidlineLeft);
                SetTopHorizontalRect(
                    (RectTransform)title.transform,
                    13f,
                    78f,
                    -24f,
                    34f);

                TMP_Text count = CreateText(
                    "PartyMemberCount",
                    panelObject.transform,
                    font,
                    "1/4",
                    16f,
                    new Color(0.28f, 0.34f, 0.39f),
                    TextAlignmentOptions.MidlineRight);
                SetTopHorizontalRect(
                    (RectTransform)count.transform,
                    186f,
                    45f,
                    -24f,
                    34f);

                GameObject collapseObject = CreateUiObject(
                    "PartyRosterCollapseButton",
                    panelObject.transform,
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                SetTopRightRect(
                    (RectTransform)collapseObject.transform,
                    new Vector2(-8f, -8f),
                    new Vector2(34f, 30f));
                Image collapseImage = collapseObject.GetComponent<Image>();
                SetSlicedImage(
                    collapseImage,
                    fillSprite,
                    new Color(0.73f, 0.79f, 0.84f, 1f));
                Button collapseButton = collapseObject.GetComponent<Button>();
                collapseButton.targetGraphic = collapseImage;
                collapseButton.colors = CreateButtonColors();
                TMP_Text collapseGlyph = CreateText(
                    "CollapseGlyph",
                    collapseObject.transform,
                    font,
                    "<",
                    19f,
                    new Color(0.05f, 0.24f, 0.31f),
                    TextAlignmentOptions.Center);
                Stretch((RectTransform)collapseGlyph.transform, 0f);

                var slots = new PartyRosterSlotView[GameData.MaximumPartySize];
                for (int index = 0; index < slots.Length; index++)
                {
                    slots[index] = CreateMemberSlot(
                        panelObject.transform,
                        font,
                        fillSprite,
                        outlineSprite,
                        index);
                }

                var serializedView = new SerializedObject(
                    panelObject.GetComponent<WorldMapPartyStatusView>());
                SetReference(serializedView, "panelRect", panelRect);
                SetReference(serializedView, "titleLabel", title);
                SetReference(serializedView, "memberCountLabel", count);
                SetReference(serializedView, "collapseButton", collapseButton);
                SetReference(serializedView, "collapseGlyph", collapseGlyph);
                SerializedProperty slotProperty =
                    serializedView.FindProperty("memberSlots");
                slotProperty.arraySize = slots.Length;
                for (int index = 0; index < slots.Length; index++)
                {
                    slotProperty.GetArrayElementAtIndex(index)
                        .objectReferenceValue = slots[index];
                }
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;

                CommonFantasyHudSkinInstaller.ApplyToPrefabContents(root);
                PrefabUtility.SaveAsPrefabAsset(root, UiRootPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Installed the compact four-member party roster into UIRoot.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static PartyRosterSlotView CreateMemberSlot(
            Transform parent,
            TMP_FontAsset font,
            Sprite fillSprite,
            Sprite outlineSprite,
            int index)
        {
            GameObject slotObject = CreateUiObject(
                $"PartyMemberSlot{index + 1}",
                parent,
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(PartyRosterSlotView));
            RectTransform slotRect = (RectTransform)slotObject.transform;
            slotRect.anchorMin = Vector2.up;
            slotRect.anchorMax = Vector2.up;
            slotRect.pivot = Vector2.up;
            slotRect.anchoredPosition = new Vector2(13f, -48f - index * 75f);
            slotRect.sizeDelta = new Vector2(244f, 69f);

            Image slotBackground = slotObject.GetComponent<Image>();
            SetSlicedImage(
                slotBackground,
                fillSprite,
                new Color(0.69f, 0.75f, 0.80f, 1f));
            Button button = slotObject.GetComponent<Button>();
            button.targetGraphic = slotBackground;
            button.colors = CreateButtonColors();

            Image outline = CreateImage(
                "SelectionOutline",
                slotObject.transform,
                outlineSprite,
                new Color(0.93f, 0.66f, 0.16f, 1f));
            Stretch((RectTransform)outline.transform, 0f);
            outline.raycastTarget = false;
            outline.enabled = false;

            GameObject portraitFrameObject = CreateUiObject(
                "PortraitFrame",
                slotObject.transform,
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform portraitFrameRect =
                (RectTransform)portraitFrameObject.transform;
            portraitFrameRect.anchorMin = new Vector2(0f, 0.5f);
            portraitFrameRect.anchorMax = portraitFrameRect.anchorMin;
            portraitFrameRect.pivot = new Vector2(0.5f, 0.5f);
            portraitFrameRect.anchoredPosition = new Vector2(34f, 0f);
            portraitFrameRect.sizeDelta = new Vector2(56f, 56f);
            Image portraitFrame = portraitFrameObject.GetComponent<Image>();
            SetSlicedImage(
                portraitFrame,
                fillSprite,
                new Color(0.83f, 0.86f, 0.88f, 1f));
            portraitFrame.raycastTarget = false;

            GameObject portraitObject = CreateUiObject(
                "Portrait",
                portraitFrameObject.transform,
                typeof(CanvasRenderer),
                typeof(RawImage));
            Stretch((RectTransform)portraitObject.transform, 5f);
            RawImage portrait = portraitObject.GetComponent<RawImage>();
            portrait.raycastTarget = false;
            portrait.enabled = false;

            GameObject expandedContent = CreateUiObject(
                "ExpandedContent",
                slotObject.transform);
            RectTransform contentRect = (RectTransform)expandedContent.transform;
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(70f, 5f);
            contentRect.offsetMax = new Vector2(-10f, -5f);

            TMP_Text name = CreateText(
                "MemberName",
                expandedContent.transform,
                font,
                "空位",
                18f,
                new Color(0.05f, 0.24f, 0.31f),
                TextAlignmentOptions.MidlineLeft);
            SetNormalizedRect(
                (RectTransform)name.transform,
                new Vector2(0f, 0.60f),
                new Vector2(0.70f, 1f));

            TMP_Text health = CreateText(
                "HealthText",
                expandedContent.transform,
                font,
                string.Empty,
                13f,
                new Color(0.14f, 0.16f, 0.19f),
                TextAlignmentOptions.MidlineRight);
            SetNormalizedRect(
                (RectTransform)health.transform,
                new Vector2(0.70f, 0.61f),
                new Vector2(1f, 1f));

            Image healthBackground = CreateImage(
                "HealthBar",
                expandedContent.transform,
                fillSprite,
                new Color(0.45f, 0.50f, 0.54f, 1f));
            SetNormalizedRect(
                (RectTransform)healthBackground.transform,
                new Vector2(0f, 0.44f),
                new Vector2(1f, 0.55f));
            Image healthFill = CreateImage(
                "HealthFill",
                healthBackground.transform,
                fillSprite,
                new Color(0.15f, 0.55f, 0.31f, 1f));
            Stretch((RectTransform)healthFill.transform, 0f);
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            healthFill.fillOrigin = 0;
            healthFill.fillAmount = 0f;

            TMP_Text locationState = CreateText(
                "LocationStateText",
                expandedContent.transform,
                font,
                "等待成员加入",
                13f,
                new Color(0.28f, 0.34f, 0.39f),
                TextAlignmentOptions.MidlineLeft);
            SetNormalizedRect(
                (RectTransform)locationState.transform,
                new Vector2(0f, 0f),
                new Vector2(0.88f, 0.38f));

            Image dot = CreateImage(
                "AvailabilityDot",
                expandedContent.transform,
                fillSprite,
                new Color(0.52f, 0.57f, 0.62f, 1f));
            RectTransform dotRect = (RectTransform)dot.transform;
            dotRect.anchorMin = new Vector2(1f, 0.18f);
            dotRect.anchorMax = dotRect.anchorMin;
            dotRect.pivot = new Vector2(1f, 0.5f);
            dotRect.anchoredPosition = Vector2.zero;
            dotRect.sizeDelta = new Vector2(11f, 11f);

            var serializedSlot = new SerializedObject(
                slotObject.GetComponent<PartyRosterSlotView>());
            SetReference(serializedSlot, "portraitImage", portrait);
            SetReference(serializedSlot, "expandedContent", expandedContent);
            SetReference(serializedSlot, "nameLabel", name);
            SetReference(serializedSlot, "healthLabel", health);
            SetReference(serializedSlot, "locationStateLabel", locationState);
            SetReference(serializedSlot, "healthFill", healthFill);
            SetReference(serializedSlot, "availabilityDot", dot);
            SetReference(serializedSlot, "selectionOutline", outline);
            serializedSlot.ApplyModifiedPropertiesWithoutUndo();
            return slotObject.GetComponent<PartyRosterSlotView>();
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
            TextAlignmentOptions alignment)
        {
            GameObject gameObject = CreateUiObject(
                name,
                parent,
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            TextMeshProUGUI label = gameObject.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
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
            Sprite sprite,
            Color color)
        {
            GameObject gameObject = CreateUiObject(
                name,
                parent,
                typeof(CanvasRenderer),
                typeof(Image));
            Image image = gameObject.GetComponent<Image>();
            SetSlicedImage(image, sprite, color);
            image.raycastTarget = false;
            return image;
        }

        private static void SetSlicedImage(Image image, Sprite sprite, Color color)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
        }

        private static ColorBlock CreateButtonColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
            colors.pressedColor = new Color(0.82f, 0.86f, 0.90f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.55f);
            colors.fadeDuration = 0.08f;
            return colors;
        }

        private static void SetTopHorizontalRect(
            RectTransform rect,
            float left,
            float right,
            float y,
            float height)
        {
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2((left - right) * 0.5f, y);
            rect.sizeDelta = new Vector2(-(left + right), height);
        }

        private static void SetTopRightRect(
            RectTransform rect,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void SetNormalizedRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = -Vector2.one * inset;
        }

        private static void SetReference(
            SerializedObject serialized,
            string name,
            Object value)
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
