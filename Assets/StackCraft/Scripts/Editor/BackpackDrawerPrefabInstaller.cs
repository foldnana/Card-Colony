#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CryingSnow.StackCraft.EditorTools
{
    [InitializeOnLoad]
    public static class BackpackDrawerPrefabInstaller
    {
        private const string UiRootPath =
            "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";
        private const string VersionMarker = "BackpackSidebarPageV2";

        static BackpackDrawerPrefabInstaller()
        {
            if (!Application.isBatchMode)
                EditorApplication.delayCall += EnsureInstalled;
        }

        [MenuItem("Tools/StackCraft/Install Backpack Sidebar UI")]
        public static void Install()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UiRootPath);
            try
            {
                Transform backpackRoot = Require(root.transform, "BackpackRoot");
                Transform menuPanel = Require(root.transform, "MenuPanel");
                Transform header = Require(menuPanel, "Header");
                BackpackView view = backpackRoot.GetComponent<BackpackView>();
                RectTransform drawer = (RectTransform)Require(
                    root.transform,
                    "BackpackTablePanel");
                Toggle tabToggle = EnsureSidebarToggle(header);
                Toggle fallbackToggle = Require(header, "LocationToggle")
                    .GetComponent<Toggle>();
                TMP_Text capacity = Require(
                    drawer,
                    "BackpackCapacityText").GetComponent<TMP_Text>();
                Button arrangeButton = Require(
                    drawer,
                    "BackpackArrangeButton").GetComponent<Button>();
                RectTransform slots = (RectTransform)Require(
                    drawer,
                    "BackpackSlots");
                RectTransform dragLayer = (RectTransform)Require(
                    backpackRoot,
                    "BackpackDragLayer");

                ConfigureSidebarPage(drawer, menuPanel, capacity);
                ConfigureDrawerButton(
                    arrangeButton,
                    drawer,
                    new Vector2(-12f, 12f),
                    new Vector2(120f, 44f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Color(0.67f, 0.43f, 0.08f, 1f),
                    new Color(0.88f, 0.64f, 0.18f, 1f),
                    new Color(0.10f, 0.08f, 0.04f, 1f));
                ConfigureSlots(slots);
                EnsurePickupHint(drawer, capacity.font);

                TMP_FontAsset font = capacity.font;
                ConfigureDragLayer(dragLayer);
                EnsureWorldCardDragPreview(
                    dragLayer,
                    font,
                    out RectTransform worldCardPreview,
                    out Image worldCardHeader,
                    out RawImage worldCardArt,
                    out TMP_Text worldCardTitle);
                RectTransform details = EnsureDetailsPanel(drawer);
                TMP_Text selectedName = EnsureText(
                    details,
                    "BackpackSelectedName",
                    font,
                    24f,
                    new Color(0.35f, 0.83f, 0.96f, 1f));
                TMP_Text selectedType = EnsureText(
                    details,
                    "BackpackSelectedType",
                    font,
                    17f,
                    new Color(0.92f, 0.72f, 0.30f, 1f));
                TMP_Text selectedDescription = EnsureText(
                    details,
                    "BackpackSelectedDescription",
                    font,
                    16f,
                    new Color(0.88f, 0.90f, 0.92f, 1f));
                ConfigureDetailsText(
                    selectedName,
                    10f,
                    32f);
                ConfigureDetailsText(
                    selectedType,
                    46f,
                    24f);
                ConfigureDetailsText(
                    selectedDescription,
                    76f,
                    90f);
                selectedName.text = "选择一个物品";
                selectedType.text = string.Empty;
                selectedDescription.text = "拖到场地取出；拖到其他格子交换位置。";
                selectedDescription.enableWordWrapping = true;

                EnsureMarker(backpackRoot);
                var serialized = new SerializedObject(view);
                SetReference(serialized, "openButton", null);
                SetReference(serialized, "openButtonLabel", null);
                SetReference(serialized, "tabToggle", tabToggle);
                SetReference(serialized, "fallbackToggle", fallbackToggle);
                SetReference(serialized, "tablePanel", drawer);
                SetReference(serialized, "capacityLabel", capacity);
                SetReference(serialized, "closeButton", null);
                SetReference(serialized, "arrangeButton", arrangeButton);
                SetReference(serialized, "slotsRoot", slots);
                SetReference(serialized, "dragLayer", dragLayer);
                SetReference(serialized, "selectedNameLabel", selectedName);
                SetReference(serialized, "selectedTypeLabel", selectedType);
                SetReference(
                    serialized,
                    "selectedDescriptionLabel",
                    selectedDescription);
                SetReference(serialized, "worldCardDragPreview", worldCardPreview);
                SetReference(serialized, "worldCardDragHeader", worldCardHeader);
                SetReference(serialized, "worldCardDragArt", worldCardArt);
                SetReference(serialized, "worldCardDragTitle", worldCardTitle);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                DestroyIfPresent(root.transform, "BackpackButton");
                DestroyIfPresent(root.transform, "BackpackCloseButton");
                DestroyIfPresent(backpackRoot, "BackpackDrawerLayoutV1");
                drawer.gameObject.SetActive(false);
                CommonFantasyHudSkinInstaller.ApplyToPrefabContents(root);
                PrefabUtility.SaveAsPrefabAsset(root, UiRootPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Backpack sidebar page serialized into UIRoot.prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/StackCraft/Validate Backpack Sidebar UI")]
        public static void Validate()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(UiRootPath);
            try
            {
                Transform backpackRoot = Require(root.transform, "BackpackRoot");
                Transform menuPanel = Require(root.transform, "MenuPanel");
                Transform header = Require(menuPanel, "Header");
                RectTransform drawer = (RectTransform)Require(
                    root.transform,
                    "BackpackTablePanel");
                GridLayoutGroup grid = Require(
                    drawer,
                    "BackpackSlots").GetComponent<GridLayoutGroup>();
                Toggle tabToggle = Require(
                    header,
                    "BackpackToggle").GetComponent<Toggle>();
                Toggle locationToggle = Require(
                    header,
                    "LocationToggle").GetComponent<Toggle>();
                BackpackView view = backpackRoot.GetComponent<BackpackView>();
                var serialized = new SerializedObject(view);

                Require(backpackRoot, VersionMarker);
                Require(drawer, "BackpackSelectedDetails");
                Require(drawer, "BackpackSelectedName");
                Require(drawer, "BackpackSelectedType");
                Require(drawer, "BackpackSelectedDescription");
                Require(drawer, "BackpackPickupHint");
                Transform dragLayer = Require(root.transform, "BackpackDragLayer");
                Transform worldPreview = Require(
                    dragLayer,
                    "BackpackWorldCardDragPreview");
                Canvas dragCanvas = dragLayer.GetComponent<Canvas>();
                if (dragCanvas == null || !dragCanvas.overrideSorting ||
                    dragCanvas.sortingOrder < 100 || worldPreview.gameObject.activeSelf)
                {
                    throw new System.InvalidOperationException(
                        "Backpack world-card drag preview is not configured correctly.");
                }
                if (drawer.parent != menuPanel ||
                    drawer.anchorMin != Vector2.zero ||
                    drawer.anchorMax != Vector2.one ||
                    drawer.anchoredPosition != new Vector2(0f, -30f) ||
                    drawer.sizeDelta != new Vector2(0f, -60f))
                {
                    throw new System.InvalidOperationException(
                        "Backpack sidebar page is not serialized correctly.");
                }
                if (grid.constraintCount != 2)
                {
                    throw new System.InvalidOperationException(
                        "Backpack drawer must use two icon columns.");
                }
                if (grid.cellSize.x < 168f || grid.cellSize.y < 140f)
                {
                    throw new System.InvalidOperationException(
                        "Backpack drawer item cells are too small.");
                }
                if (tabToggle.group == null ||
                    tabToggle.group != locationToggle.group)
                {
                    throw new System.InvalidOperationException(
                        "Backpack tab is not part of the sidebar toggle group.");
                }
                if (Find(root.transform, "BackpackButton") != null)
                    throw new System.InvalidOperationException(
                        "Legacy backpack entry button still exists.");
                foreach (string propertyName in new[]
                         {
                             "tabToggle",
                             "fallbackToggle",
                             "tablePanel",
                             "slotsRoot",
                             "selectedNameLabel",
                             "selectedTypeLabel",
                             "selectedDescriptionLabel",
                             "worldCardDragPreview",
                             "worldCardDragHeader",
                             "worldCardDragArt",
                             "worldCardDragTitle"
                         })
                {
                    if (serialized.FindProperty(propertyName)
                            ?.objectReferenceValue == null)
                    {
                        throw new System.InvalidOperationException(
                            $"BackpackView.{propertyName} is not serialized.");
                    }
                }

                Debug.Log("Backpack sidebar prefab validation passed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureInstalled()
        {
            EditorApplication.delayCall -= EnsureInstalled;
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(UiRootPath))
            {
                return;
            }

            string yaml = File.ReadAllText(UiRootPath);
            if (!yaml.Contains("m_Name: " + VersionMarker))
                Install();
        }

        private static Toggle EnsureSidebarToggle(Transform header)
        {
            Transform existing = Find(header, "BackpackToggle");
            GameObject toggleObject;
            if (existing != null)
            {
                toggleObject = existing.gameObject;
            }
            else
            {
                GameObject locationToggle = Require(
                    header,
                    "LocationToggle").gameObject;
                toggleObject = Object.Instantiate(locationToggle, header);
                toggleObject.name = "BackpackToggle";
            }

            toggleObject.transform.SetParent(header, false);
            toggleObject.transform.SetAsLastSibling();
            Toggle toggle = toggleObject.GetComponent<Toggle>();
            Toggle location = Require(header, "LocationToggle")
                .GetComponent<Toggle>();
            toggle.group = location.group;
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.SetIsOnWithoutNotify(false);

            TMP_Text label = toggleObject.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
                throw new System.InvalidOperationException(
                    "BackpackToggle is missing its text label.");
            label.text = "背包";
            return toggle;
        }

        private static TMP_Text EnsurePickupHint(
            RectTransform drawer,
            TMP_FontAsset font)
        {
            TMP_Text hint = EnsureText(
                drawer,
                "BackpackPickupHint",
                font,
                18f,
                new Color(0.62f, 0.84f, 0.93f, 1f));
            RectTransform hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 1f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0f, -48f);
            hintRect.sizeDelta = new Vector2(-28f, 28f);
            hintRect.localScale = Vector3.one;
            hint.text = "拖入背包 · 拖动图标换位";
            hint.alignment = TextAlignmentOptions.MidlineLeft;
            hint.enableWordWrapping = false;
            return hint;
        }

        private static void ConfigureSidebarPage(
            RectTransform drawer,
            Transform menuPanel,
            TMP_Text capacity)
        {
            drawer.SetParent(menuPanel, false);
            drawer.anchorMin = Vector2.zero;
            drawer.anchorMax = Vector2.one;
            drawer.pivot = new Vector2(0.5f, 0.5f);
            drawer.anchoredPosition = new Vector2(0f, -30f);
            drawer.sizeDelta = new Vector2(0f, -60f);
            drawer.localScale = Vector3.one;
            Image panelImage = drawer.GetComponent<Image>();
            panelImage.enabled = true;
            Image locationPage = Require(menuPanel, "LocationView")
                .GetComponent<Image>();
            panelImage.sprite = locationPage.sprite;
            panelImage.type = locationPage.type;
            panelImage.color = locationPage.color;
            panelImage.raycastTarget = true;
            ConfigureOutline(
                drawer.gameObject,
                new Color(0.42f, 0.55f, 0.66f, 0.92f),
                new Vector2(2f, -2f));

            Transform oldBackground = Find(drawer, "BackpackBackground");
            if (oldBackground != null)
                Object.DestroyImmediate(oldBackground.gameObject);

            SetAnchored(
                capacity.rectTransform,
                new Vector2(14f, -7f),
                new Vector2(240f, 38f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f));
            capacity.fontSize = 26f;
            capacity.color = new Color(0.92f, 0.94f, 0.96f, 1f);
            capacity.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private static void ConfigureEntryButton(
            Button button,
            TMP_Text label)
        {
            SetAnchored(
                (RectTransform)button.transform,
                new Vector2(30f, 26f),
                new Vector2(188f, 50f),
                Vector2.zero,
                Vector2.zero);
            StyleFlatButton(
                button,
                new Color(0.25f, 0.12f, 0.42f, 0.97f),
                new Color(0.55f, 0.30f, 0.78f, 1f),
                Color.white);
            label.fontSize = 21f;
            label.alignment = TextAlignmentOptions.Center;
        }

        private static void ConfigureDrawerButton(
            Button button,
            RectTransform drawer,
            Vector2 position,
            Vector2 size,
            Vector2 anchor,
            Vector2 pivot,
            Color normal,
            Color highlighted,
            Color textColor)
        {
            button.transform.SetParent(drawer, false);
            SetAnchored(
                (RectTransform)button.transform,
                position,
                size,
                anchor,
                pivot);
            StyleFlatButton(button, normal, highlighted, textColor);
        }

        private static void ConfigureDragLayer(RectTransform dragLayer)
        {
            dragLayer.SetAsLastSibling();
            Canvas canvas = dragLayer.GetComponent<Canvas>();
            if (canvas == null)
                canvas = dragLayer.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;

            CanvasGroup group = dragLayer.GetComponent<CanvasGroup>();
            if (group == null)
                group = dragLayer.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private static void EnsureWorldCardDragPreview(
            RectTransform dragLayer,
            TMP_FontAsset font,
            out RectTransform preview,
            out Image header,
            out RawImage art,
            out TMP_Text title)
        {
            Transform existing = Find(dragLayer, "BackpackWorldCardDragPreview");
            GameObject previewObject;
            if (existing == null)
            {
                previewObject = new GameObject(
                    "BackpackWorldCardDragPreview",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup));
                previewObject.transform.SetParent(dragLayer, false);
            }
            else
            {
                previewObject = existing.gameObject;
            }

            preview = (RectTransform)previewObject.transform;
            SetAnchored(
                preview,
                Vector2.zero,
                new Vector2(120f, 156f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            Image body = previewObject.GetComponent<Image>();
            body.sprite = null;
            body.type = Image.Type.Simple;
            body.color = new Color(0.91f, 0.90f, 0.86f, 1f);
            body.raycastTarget = false;
            ConfigureOutline(
                previewObject,
                new Color(0.16f, 0.83f, 0.98f, 0.96f),
                new Vector2(2f, -2f));
            CanvasGroup group = previewObject.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            Transform headerTransform = Find(preview, "Header");
            GameObject headerObject;
            if (headerTransform == null)
            {
                headerObject = new GameObject(
                    "Header",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                headerObject.transform.SetParent(preview, false);
            }
            else
            {
                headerObject = headerTransform.gameObject;
            }
            header = headerObject.GetComponent<Image>();
            header.sprite = null;
            header.color = new Color(0.48f, 0.38f, 0.30f, 1f);
            header.raycastTarget = false;
            SetAnchored(
                header.rectTransform,
                new Vector2(0f, -15f),
                new Vector2(0f, 30f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f));
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(1f, 1f);

            title = EnsureText(
                header.rectTransform,
                "Title",
                font,
                17f,
                Color.white);
            SetStretch(title.rectTransform, 6f, 6f, 0f, 0f);
            title.alignment = TextAlignmentOptions.Center;
            title.enableWordWrapping = false;

            Transform artTransform = Find(preview, "Art");
            GameObject artObject;
            if (artTransform == null)
            {
                artObject = new GameObject(
                    "Art",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage));
                artObject.transform.SetParent(preview, false);
            }
            else
            {
                artObject = artTransform.gameObject;
            }
            art = artObject.GetComponent<RawImage>();
            art.texture = null;
            art.color = Color.white;
            art.raycastTarget = false;
            SetAnchored(
                art.rectTransform,
                new Vector2(0f, -12f),
                new Vector2(98f, 98f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));

            preview.SetAsLastSibling();
            previewObject.SetActive(false);
        }

        private static void ConfigureSlots(RectTransform slots)
        {
            RectTransform viewport = slots.parent as RectTransform;
            SetStretch(viewport, 12f, 12f, 256f, 80f);
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = null;
            viewportImage.color = new Color(0.01f, 0.025f, 0.04f, 0.36f);

            slots.anchorMin = new Vector2(0f, 1f);
            slots.anchorMax = new Vector2(1f, 1f);
            slots.pivot = new Vector2(0.5f, 1f);
            slots.anchoredPosition = new Vector2(0f, -8f);
            slots.sizeDelta = new Vector2(-16f, 502f);
            GridLayoutGroup grid = slots.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(172f, 146f);
            grid.spacing = new Vector2(10f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;

            foreach (Transform slot in slots)
            {
                Image image = slot.GetComponent<Image>();
                if (image == null)
                    continue;
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = new Color(0.018f, 0.045f, 0.07f, 0.92f);
                image.raycastTarget = false;
                ConfigureOutline(
                    slot.gameObject,
                    new Color(0.24f, 0.34f, 0.42f, 0.72f),
                    new Vector2(1f, -1f));
            }
        }

        private static RectTransform EnsureDetailsPanel(RectTransform drawer)
        {
            Transform existing = Find(drawer, "BackpackSelectedDetails");
            GameObject detailsObject;
            if (existing == null)
            {
                detailsObject = new GameObject(
                    "BackpackSelectedDetails",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                detailsObject.transform.SetParent(drawer, false);
            }
            else
            {
                detailsObject = existing.gameObject;
            }

            RectTransform details = (RectTransform)detailsObject.transform;
            details.anchorMin = Vector2.zero;
            details.anchorMax = new Vector2(1f, 0f);
            details.pivot = Vector2.zero;
            details.anchoredPosition = new Vector2(12f, 64f);
            details.sizeDelta = new Vector2(-24f, 180f);
            details.localScale = Vector3.one;
            Image image = details.GetComponent<Image>();
            image.sprite = null;
            image.color = new Color(0.035f, 0.12f, 0.18f, 0.98f);
            image.raycastTarget = false;
            ConfigureOutline(
                detailsObject,
                new Color(0.30f, 0.43f, 0.53f, 0.82f),
                new Vector2(1f, -1f));
            return details;
        }

        private static TMP_Text EnsureText(
            RectTransform parent,
            string name,
            TMP_FontAsset font,
            float fontSize,
            Color color)
        {
            Transform existing = Find(parent, name);
            TMP_Text text;
            if (existing == null)
            {
                GameObject textObject = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                textObject.transform.SetParent(parent, false);
                text = textObject.GetComponent<TMP_Text>();
            }
            else
            {
                text = existing.GetComponent<TMP_Text>();
            }
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            return text;
        }

        private static void ConfigureDetailsText(
            TMP_Text text,
            float top,
            float height)
        {
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(-24f, height);
            rect.localScale = Vector3.one;
        }

        private static void StyleFlatButton(
            Button button,
            Color normal,
            Color highlighted,
            Color textColor)
        {
            Image image = button.GetComponent<Image>();
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            button.targetGraphic = image;
            foreach (Shadow shadow in button.GetComponents<Shadow>()
                         .Where(component => component.GetType() == typeof(Shadow))
                         .ToArray())
            {
                Object.DestroyImmediate(shadow);
            }
            ConfigureOutline(
                button.gameObject,
                new Color(highlighted.r, highlighted.g, highlighted.b, 0.84f),
                new Vector2(1f, -1f));
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = Color.Lerp(normal, Color.black, 0.18f);
            colors.selectedColor = highlighted;
            colors.disabledColor = new Color(0.35f, 0.38f, 0.42f, 0.72f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = textColor;
                label.fontSize = 18f;
            }
        }

        private static void ConfigureOutline(
            GameObject target,
            Color color,
            Vector2 distance)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
                outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }

        private static void EnsureMarker(Transform backpackRoot)
        {
            if (Find(backpackRoot, VersionMarker) != null)
                return;
            var marker = new GameObject(VersionMarker, typeof(RectTransform));
            marker.transform.SetParent(backpackRoot, false);
            marker.SetActive(false);
        }

        private static void DestroyIfPresent(Transform root, string name)
        {
            Transform target = Find(root, name);
            if (target != null)
                Object.DestroyImmediate(target.gameObject);
        }

        private static Transform Require(Transform root, string name)
        {
            Transform result = Find(root, name);
            if (result == null)
            {
                throw new System.InvalidOperationException(
                    $"{UiRootPath} is missing {name}.");
            }
            return result;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = Find(root.GetChild(index), name);
                if (found != null)
                    return found;
            }
            return null;
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

        private static void SetStretch(
            RectTransform rect,
            float left,
            float right,
            float bottom,
            float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        private static void SetReference(
            SerializedObject serialized,
            string propertyName,
            Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new System.InvalidOperationException(
                    $"BackpackView is missing serialized field {propertyName}.");
            }
            property.objectReferenceValue = value;
        }
    }
}
#endif
