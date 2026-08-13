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
        private const string VersionMarker = "BackpackSidebarPageV3";

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
                EnsureCharacterEquipmentArea(
                    drawer,
                    font,
                    out TMP_Text characterName,
                    out TMP_Text characterHealth,
                    out RawImage characterPortrait,
                    out Button previousCharacter,
                    out Button nextCharacter,
                    out RectTransform equipmentSlots,
                    out BackpackEquipmentSlotView equipmentSlotTemplate);
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

                Button equipButton = EnsureButton(
                    details,
                    "BackpackEquipmentActionButton",
                    font,
                    "装备");
                SetAnchored(
                    (RectTransform)equipButton.transform,
                    new Vector2(-10f, 10f),
                    new Vector2(104f, 38f),
                    Vector2.one,
                    Vector2.one);
                StyleFlatButton(
                    equipButton,
                    new Color(0.20f, 0.54f, 0.40f, 1f),
                    new Color(0.28f, 0.66f, 0.50f, 1f),
                    Color.white);
                equipButton.gameObject.SetActive(false);

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
                SetReference(serialized, "characterNameLabel", characterName);
                SetReference(serialized, "characterHealthLabel", characterHealth);
                SetReference(serialized, "characterPortrait", characterPortrait);
                SetReference(serialized, "previousCharacterButton", previousCharacter);
                SetReference(serialized, "nextCharacterButton", nextCharacter);
                SetReference(serialized, "equipmentSlotsRoot", equipmentSlots);
                SetReference(serialized, "equipmentSlotTemplate", equipmentSlotTemplate);
                SetReference(serialized, "equipButton", equipButton);
                SetReference(
                    serialized,
                    "equipButtonLabel",
                    equipButton.GetComponentInChildren<TMP_Text>(true));
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
                Require(drawer, "BackpackCharacterHeader");
                Require(drawer, "BackpackEquipmentViewport");
                Require(drawer, "BackpackEquipmentSlots");
                Require(drawer, "EquipmentSlotTemplate");
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
                    drawer.offsetMin != new Vector2(14f, 14f) ||
                    drawer.offsetMax != new Vector2(-14f, -72f))
                {
                    throw new System.InvalidOperationException(
                        "Backpack sidebar page is not serialized correctly.");
                }
                if (grid.constraintCount != 3)
                {
                    throw new System.InvalidOperationException(
                        "Backpack drawer must use three compact icon columns.");
                }
                if (grid.cellSize.x < 96f || grid.cellSize.x > 116f ||
                    grid.cellSize.y < 88f || grid.cellSize.y > 112f)
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
                             "characterNameLabel",
                             "characterHealthLabel",
                             "characterPortrait",
                             "previousCharacterButton",
                             "nextCharacterButton",
                             "equipmentSlotsRoot",
                             "equipmentSlotTemplate",
                             "equipButton",
                             "equipButtonLabel",
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
            drawer.offsetMin = new Vector2(14f, 14f);
            drawer.offsetMax = new Vector2(-14f, -72f);
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
            SetStretch(viewport, 12f, 12f, 220f, 330f);
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = null;
            viewportImage.color = new Color(0.01f, 0.025f, 0.04f, 0.36f);

            slots.anchorMin = new Vector2(0f, 1f);
            slots.anchorMax = new Vector2(1f, 1f);
            slots.pivot = new Vector2(0.5f, 1f);
            slots.anchoredPosition = new Vector2(0f, -8f);
            slots.sizeDelta = new Vector2(-16f, 420f);
            GridLayoutGroup grid = slots.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(106f, 98f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
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

        private static void EnsureCharacterEquipmentArea(
            RectTransform drawer,
            TMP_FontAsset font,
            out TMP_Text characterName,
            out TMP_Text characterHealth,
            out RawImage characterPortrait,
            out Button previousCharacter,
            out Button nextCharacter,
            out RectTransform equipmentSlots,
            out BackpackEquipmentSlotView equipmentSlotTemplate)
        {
            RectTransform header = EnsurePanel(
                drawer,
                "BackpackCharacterHeader",
                new Color(0.16f, 0.25f, 0.33f, 0.96f));
            SetAnchored(
                header,
                new Vector2(12f, -50f),
                new Vector2(-24f, 66f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f));
            header.anchorMax = new Vector2(1f, 1f);

            previousCharacter = EnsureButton(
                header,
                "BackpackPreviousCharacter",
                font,
                "<");
            SetAnchored(
                (RectTransform)previousCharacter.transform,
                new Vector2(8f, -8f),
                new Vector2(38f, 50f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f));
            nextCharacter = EnsureButton(
                header,
                "BackpackNextCharacter",
                font,
                ">");
            SetAnchored(
                (RectTransform)nextCharacter.transform,
                new Vector2(-8f, -8f),
                new Vector2(38f, 50f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f));

            RectTransform portraitRect = EnsurePanel(
                header,
                "BackpackCharacterPortraitFrame",
                new Color(0.82f, 0.87f, 0.91f, 1f));
            SetAnchored(
                portraitRect,
                new Vector2(54f, -8f),
                new Vector2(50f, 50f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f));
            Transform portraitTransform = Find(portraitRect, "Portrait");
            if (portraitTransform == null)
            {
                var portraitObject = new GameObject(
                    "Portrait",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage));
                portraitObject.transform.SetParent(portraitRect, false);
                characterPortrait = portraitObject.GetComponent<RawImage>();
            }
            else
                characterPortrait = portraitTransform.GetComponent<RawImage>();
            SetStretch(characterPortrait.rectTransform, 4f, 4f, 4f, 4f);
            characterPortrait.raycastTarget = false;

            characterName = EnsureText(
                header,
                "BackpackCharacterName",
                font,
                19f,
                new Color(0.90f, 0.94f, 0.97f, 1f));
            SetAnchored(
                characterName.rectTransform,
                new Vector2(114f, -8f),
                new Vector2(-168f, 26f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f));
            characterName.rectTransform.anchorMax = new Vector2(1f, 1f);
            characterHealth = EnsureText(
                header,
                "BackpackCharacterHealth",
                font,
                14f,
                new Color(0.68f, 0.78f, 0.86f, 1f));
            SetAnchored(
                characterHealth.rectTransform,
                new Vector2(114f, -35f),
                new Vector2(-168f, 20f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f));
            characterHealth.rectTransform.anchorMax = new Vector2(1f, 1f);

            TMP_Text equipmentTitle = EnsureText(
                drawer,
                "BackpackEquipmentTitle",
                font,
                17f,
                new Color(0.88f, 0.92f, 0.95f, 1f));
            equipmentTitle.text = "当前装备  ·  横向滑动";
            SetAnchored(
                equipmentTitle.rectTransform,
                new Vector2(14f, -122f),
                new Vector2(-28f, 26f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f));
            equipmentTitle.rectTransform.anchorMax = new Vector2(1f, 1f);

            RectTransform viewport = EnsurePanel(
                drawer,
                "BackpackEquipmentViewport",
                new Color(0.08f, 0.14f, 0.19f, 0.82f));
            if (viewport.GetComponent<RectMask2D>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = viewport.GetComponent<ScrollRect>();
            if (scroll == null)
                scroll = viewport.gameObject.AddComponent<ScrollRect>();
            SetAnchored(
                viewport,
                new Vector2(12f, -150f),
                new Vector2(-24f, 104f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f));
            viewport.anchorMax = new Vector2(1f, 1f);

            RectTransform content = EnsurePanel(
                viewport,
                "BackpackEquipmentSlots",
                Color.clear);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = new Vector2(8f, 0f);
            content.sizeDelta = new Vector2(380f, -12f);
            HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
                layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 8, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            equipmentSlots = content;

            foreach (Transform child in content.Cast<Transform>().ToArray())
            {
                if (child.name.StartsWith("EquipmentSlot_"))
                    Object.DestroyImmediate(child.gameObject);
            }

            Transform templateTransform = Find(content, "EquipmentSlotTemplate");
            GameObject template;
            if (templateTransform == null)
            {
                template = new GameObject(
                    "EquipmentSlotTemplate",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(LayoutElement),
                    typeof(BackpackEquipmentSlotView));
                template.transform.SetParent(content, false);
            }
            else
                template = templateTransform.gameObject;
            RectTransform templateRect = (RectTransform)template.transform;
            templateRect.sizeDelta = new Vector2(96f, 88f);
            LayoutElement element = template.GetComponent<LayoutElement>();
            element.preferredWidth = 96f;
            element.preferredHeight = 88f;
            Image templateImage = template.GetComponent<Image>();
            templateImage.color = new Color(0.20f, 0.30f, 0.39f, 1f);
            templateImage.raycastTarget = true;
            Outline outline = template.GetComponent<Outline>();
            if (outline == null)
                outline = template.AddComponent<Outline>();
            outline.effectColor = new Color(0.94f, 0.68f, 0.22f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.enabled = false;

            RawImage art = EnsureRawImage(templateRect, "Art");
            SetAnchored(
                art.rectTransform,
                new Vector2(0f, -5f),
                new Vector2(44f, 44f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f));
            TMP_Text itemName = EnsureText(
                templateRect,
                "ItemName",
                font,
                14f,
                Color.white);
            SetAnchored(
                itemName.rectTransform,
                new Vector2(4f, -51f),
                new Vector2(-8f, 18f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f));
            itemName.rectTransform.anchorMax = new Vector2(1f, 1f);
            itemName.alignment = TextAlignmentOptions.Center;
            TMP_Text slotName = EnsureText(
                templateRect,
                "SlotName",
                font,
                11f,
                new Color(0.69f, 0.79f, 0.87f, 1f));
            SetAnchored(
                slotName.rectTransform,
                new Vector2(4f, -69f),
                new Vector2(-8f, 16f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f));
            slotName.rectTransform.anchorMax = new Vector2(1f, 1f);
            slotName.alignment = TextAlignmentOptions.Center;
            equipmentSlotTemplate = template.GetComponent<BackpackEquipmentSlotView>();
            var slotSerialized = new SerializedObject(equipmentSlotTemplate);
            SetReference(slotSerialized, "art", art);
            SetReference(slotSerialized, "itemNameLabel", itemName);
            SetReference(slotSerialized, "slotNameLabel", slotName);
            SetReference(slotSerialized, "selectionOutline", outline);
            slotSerialized.ApplyModifiedPropertiesWithoutUndo();
            template.SetActive(false);

            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                GameObject previewSlot = Object.Instantiate(template, content);
                previewSlot.name = $"EquipmentSlot_{slot}";
                previewSlot.SetActive(true);
                previewSlot.GetComponentInChildren<TMP_Text>(true).text = "+";
                TMP_Text[] labels = previewSlot.GetComponentsInChildren<TMP_Text>(true);
                if (labels.Length > 1)
                    labels[1].text = BackpackEquipmentSlotView.SlotLabel(slot);
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
            details.anchoredPosition = new Vector2(12f, 58f);
            details.sizeDelta = new Vector2(-24f, 150f);
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

        private static RectTransform EnsurePanel(
            RectTransform parent,
            string name,
            Color color)
        {
            Transform existing = Find(parent, name);
            GameObject panelObject;
            if (existing == null)
            {
                panelObject = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                panelObject.transform.SetParent(parent, false);
            }
            else
                panelObject = existing.gameObject;
            Image image = panelObject.GetComponent<Image>();
            image.sprite = null;
            image.color = color;
            image.raycastTarget = color.a > 0f;
            return (RectTransform)panelObject.transform;
        }

        private static Button EnsureButton(
            RectTransform parent,
            string name,
            TMP_FontAsset font,
            string label)
        {
            Transform existing = Find(parent, name);
            GameObject buttonObject;
            if (existing == null)
            {
                buttonObject = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                buttonObject.transform.SetParent(parent, false);
            }
            else
                buttonObject = existing.gameObject;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            TMP_Text text = EnsureText(
                (RectTransform)buttonObject.transform,
                "Label",
                font,
                17f,
                Color.white);
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            SetStretch(text.rectTransform, 0f, 0f, 0f, 0f);
            return button;
        }

        private static RawImage EnsureRawImage(
            RectTransform parent,
            string name)
        {
            Transform existing = Find(parent, name);
            if (existing != null)
                return existing.GetComponent<RawImage>();
            var imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            imageObject.transform.SetParent(parent, false);
            RawImage image = imageObject.GetComponent<RawImage>();
            image.raycastTarget = false;
            return image;
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
