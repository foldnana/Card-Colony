#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class BackpackUiPrefabInstaller
    {
        private const string UiRootPath = "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";
        private const string BackpackBackgroundPath =
            "Assets/StackCraft/Textures/UI/BackpackBackground.png";

        [MenuItem("Tools/StackCraft/Install Backpack Tabletop UI")]
        public static void Install()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UiRootPath);
            try
            {
                Transform statusPanel = FindDescendant(root.transform, "WorldMapPartyStatusPanel");
                Transform parent = statusPanel != null
                    ? statusPanel.parent
                    : FindDescendant(root.transform, "UICanvas");
                if (parent == null)
                    throw new System.InvalidOperationException("UIRoot screen canvas was not found.");

                Transform previous = FindDescendant(root.transform, "BackpackRoot");
                if (previous != null)
                    Object.DestroyImmediate(previous.gameObject);

                TMP_FontAsset font = FindDescendant(root.transform, "InfoText")
                    ?.GetComponent<TMP_Text>()?.font;
                if (font == null)
                    font = FindDescendant(root.transform, "PartyName")
                        ?.GetComponent<TMP_Text>()?.font;

                GameObject backpackRoot = CreateUiObject(
                    "BackpackRoot",
                    parent,
                    typeof(BackpackView));
                Stretch((RectTransform)backpackRoot.transform);

                Button openButton = CreateButton(
                    "BackpackButton",
                    backpackRoot.transform,
                    font,
                    "背包  0/8",
                    new Color(0.035f, 0.055f, 0.065f, 0.96f),
                    new Color(0.22f, 0.72f, 0.95f));
                SetBottomLeft(
                    (RectTransform)openButton.transform,
                    new Vector2(30f, 30f),
                    new Vector2(420f, 58f));

                GameObject table = CreateUiObject(
                    "BackpackTablePanel",
                    backpackRoot.transform,
                    typeof(CanvasRenderer),
                    typeof(Image));
                RectTransform tableRect = (RectTransform)table.transform;
                SetBottomLeft(tableRect, new Vector2(30f, 102f), new Vector2(720f, 440f));
                Image tableImage = table.GetComponent<Image>();
                Sprite backpackBackground = AssetDatabase.LoadAssetAtPath<Sprite>(
                    BackpackBackgroundPath);
                if (backpackBackground == null)
                {
                    throw new System.InvalidOperationException(
                        $"Backpack background sprite was not found at {BackpackBackgroundPath}.");
                }
                tableImage.sprite = backpackBackground;
                tableImage.color = Color.white;
                tableImage.preserveAspect = true;
                tableImage.raycastTarget = true;
                tableImage.enabled = false;

                GameObject background = CreateUiObject(
                    "BackpackBackground",
                    table.transform,
                    typeof(CanvasRenderer),
                    typeof(Image));
                SetAnchored(
                    (RectTransform)background.transform,
                    new Vector2(0f, -40f),
                    new Vector2(1057.5569f, 703.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f));
                Image backgroundImage = background.GetComponent<Image>();
                backgroundImage.sprite = backpackBackground;
                backgroundImage.color = Color.white;
                backgroundImage.preserveAspect = true;
                backgroundImage.raycastTarget = true;
                background.transform.SetAsFirstSibling();

                TMP_Text title = CreateText(
                    "BackpackCapacityText",
                    table.transform,
                    font,
                    "背包  0/8",
                    30f,
                    new Color(0.35f, 0.78f, 0.98f),
                    TextAlignmentOptions.Midline);
                SetAnchored(
                    title.rectTransform,
                    new Vector2(28f, -18f),
                    new Vector2(330f, 48f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f));

                Button arrangeButton = CreateButton(
                    "BackpackArrangeButton",
                    table.transform,
                    font,
                    "整理",
                    new Color(0.16f, 0.35f, 0.48f, 1f),
                    Color.white);
                SetAnchored(
                    (RectTransform)arrangeButton.transform,
                    new Vector2(-216f, -18f),
                    new Vector2(130f, 48f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f));

                Button closeButton = CreateButton(
                    "BackpackCloseButton",
                    table.transform,
                    font,
                    "关闭",
                    new Color(0.3f, 0.3f, 0.32f, 1f),
                    Color.white);
                SetAnchored(
                    (RectTransform)closeButton.transform,
                    new Vector2(-66f, -18f),
                    new Vector2(130f, 48f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f));

                GameObject viewport = CreateUiObject(
                    "BackpackScrollViewport",
                    table.transform,
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(RectMask2D),
                    typeof(ScrollRect));
                RectTransform viewportRect = (RectTransform)viewport.transform;
                SetAnchored(
                    viewportRect,
                    new Vector2(42.579628f, -97.96823f),
                    new Vector2(649.4204f, 310.0317f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f));
                Image viewportImage = viewport.GetComponent<Image>();
                viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
                viewportImage.raycastTarget = true;

                GameObject slots = CreateUiObject(
                    "BackpackSlots",
                    viewport.transform,
                    typeof(GridLayoutGroup));
                RectTransform slotsRect = (RectTransform)slots.transform;
                slotsRect.anchorMin = new Vector2(0f, 1f);
                slotsRect.anchorMax = new Vector2(1f, 1f);
                slotsRect.pivot = new Vector2(0.5f, 1f);
                slotsRect.anchoredPosition = Vector2.zero;
                slotsRect.sizeDelta = new Vector2(0f, 326f);
                slotsRect.localScale = Vector3.one;
                GridLayoutGroup grid = slots.GetComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(132.1f, 130.7f);
                grid.spacing = new Vector2(9f, 9f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 4;
                grid.childAlignment = TextAnchor.MiddleCenter;

                ScrollRect scroll = viewport.GetComponent<ScrollRect>();
                scroll.viewport = viewportRect;
                scroll.content = slotsRect;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 42f;

                for (int index = 0; index < 8; index++)
                {
                    GameObject slot = CreateUiObject(
                        $"BackpackSlot{index + 1}",
                        slots.transform,
                        typeof(CanvasRenderer),
                        typeof(Image));
                    Image slotImage = slot.GetComponent<Image>();
                    slotImage.color = new Color(0.12f, 0.14f, 0.15f, 0.72f);
                    slotImage.raycastTarget = false;
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(slotsRect);

                GameObject dragLayer = CreateUiObject("BackpackDragLayer", backpackRoot.transform);
                Stretch((RectTransform)dragLayer.transform);
                dragLayer.transform.SetAsLastSibling();

                var serialized = new SerializedObject(backpackRoot.GetComponent<BackpackView>());
                SetReference(serialized, "openButton", openButton);
                SetReference(serialized, "openButtonLabel", openButton.GetComponentInChildren<TMP_Text>(true));
                SetReference(serialized, "tablePanel", tableRect);
                SetReference(serialized, "capacityLabel", title);
                SetReference(serialized, "closeButton", closeButton);
                SetReference(serialized, "arrangeButton", arrangeButton);
                SetReference(serialized, "slotsRoot", slotsRect);
                SetReference(serialized, "dragLayer", (RectTransform)dragLayer.transform);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                table.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, UiRootPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Installed backpack tabletop UI into UIRoot.");
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
            var required = new System.Collections.Generic.List<System.Type>
            {
                typeof(RectTransform)
            };
            required.AddRange(components);
            var gameObject = new GameObject(name, required.ToArray());
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string label,
            Color background,
            Color foreground)
        {
            GameObject buttonObject = CreateUiObject(
                name,
                parent,
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            Image image = buttonObject.GetComponent<Image>();
            image.color = background;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            TMP_Text text = CreateText(
                "Label",
                buttonObject.transform,
                font,
                label,
                25f,
                foreground,
                TextAlignmentOptions.Center);
            Stretch(text.rectTransform);
            return button;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string value,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateUiObject(
                name,
                parent,
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void SetBottomLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            SetAnchored(rect, position, size, Vector2.zero, Vector2.zero);
        }

        private static void SetAnchored(
            RectTransform rect,
            Vector2 position,
            Vector2 size,
            Vector2 anchor,
            Vector2 pivot)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
                return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child;
            }
            return null;
        }

        private static void SetReference(SerializedObject serialized, string name, Object value)
        {
            serialized.FindProperty(name).objectReferenceValue = value;
        }
    }
}
#endif
