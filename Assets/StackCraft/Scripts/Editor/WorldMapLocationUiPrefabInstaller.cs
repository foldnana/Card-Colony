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
                Transform screenCanvas =
                    FindDescendant(root.transform, "UICanvas");
                Transform menuPanel = FindDescendant(root.transform, "MenuPanel");
                Transform header = FindDescendant(menuPanel, "Header");
                Toggle questsToggle = FindDescendant(header, "QuestsToggle").GetComponent<Toggle>();
                Toggle recipesToggle = FindDescendant(header, "RecipesToggle").GetComponent<Toggle>();
                Transform recipesView =
                    FindDescendant(menuPanel, "RecipesView");
                TMP_FontAsset font = questsToggle.GetComponentInChildren<TMP_Text>(true).font;

                Transform oldLocationToggle = FindDescendant(header, "LocationToggle");
                Transform oldLocationView = FindDescendant(menuPanel, "LocationView");
                Transform oldMarketButton =
                    FindDescendant(screenCanvas, "LocalMarketButton");
                Transform oldMarketModal =
                    FindDescendant(screenCanvas, "PublicMarketModal");
                PublicMarketTradeScreen oldMarketScreen =
                    root.GetComponent<PublicMarketTradeScreen>();

                bool locationSidebarInstalled =
                    oldLocationToggle != null &&
                    oldLocationView != null &&
                    oldMarketButton != null &&
                    oldMarketModal != null &&
                    oldMarketScreen != null &&
                    oldMarketModal.GetComponentInChildren<
                        MarketCommodityListItem>(true) != null;
                bool dualInventoryInstalled =
                    FindDescendant(
                        oldMarketModal,
                        "PublicMarketBackpackListContent") != null &&
                    FindDescendant(
                        oldMarketModal,
                        "PublicMarketMarketListContent") != null &&
                    FindDescendant(
                        oldMarketModal,
                        "PublicMarketTransactionPanel") != null;
                bool marketUiCurrent = dualInventoryInstalled &&
                    FindDescendant(
                        oldMarketModal,
                        "PublicMarketBackpackEmptyState") != null &&
                    FindDescendant(
                        oldMarketModal,
                        "PublicMarketMarketEmptyState") != null;
                if (locationSidebarInstalled && marketUiCurrent)
                {
                    UpgradeExistingPublicMarket(
                        root,
                        oldMarketModal,
                        font,
                        oldMarketScreen);
                    PrefabUtility.SaveAsPrefabAsset(root, UiRootPath);
                    AssetDatabase.SaveAssets();
                    Debug.Log(
                        "World-map location sidebar and public market UI " +
                        "are already installed.");
                    return;
                }
                if (locationSidebarInstalled && dualInventoryInstalled)
                {
                    UpgradeExistingPublicMarket(
                        root,
                        oldMarketModal,
                        font,
                        oldMarketScreen);
                    PrefabUtility.SaveAsPrefabAsset(root, UiRootPath);
                    AssetDatabase.SaveAssets();
                    Debug.Log(
                        "Upgraded the existing public market UI in place.");
                    return;
                }
                if (locationSidebarInstalled)
                {
                    ReplacePublicMarketWithDualInventory(
                        root,
                        screenCanvas,
                        font,
                        oldMarketButton.GetComponent<Button>());
                    PrefabUtility.SaveAsPrefabAsset(root, UiRootPath);
                    AssetDatabase.SaveAssets();
                    Debug.Log(
                        "Upgraded public market to the dual-inventory layout.");
                    return;
                }

                if (oldLocationToggle != null)
                    Object.DestroyImmediate(oldLocationToggle.gameObject);
                if (oldLocationView != null)
                    Object.DestroyImmediate(oldLocationView.gameObject);
                if (oldMarketButton != null)
                    Object.DestroyImmediate(oldMarketButton.gameObject);
                if (oldMarketModal != null)
                    Object.DestroyImmediate(oldMarketModal.gameObject);
                if (oldMarketScreen != null)
                    Object.DestroyImmediate(oldMarketScreen);

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
                    typeof(RawImage),
                    typeof(AspectRatioFitter));
                SetRect(
                    (RectTransform)artObject.transform,
                    new Vector2(0.32f, 0.67f),
                    new Vector2(0.68f, 0.87f),
                    Vector2.zero,
                    Vector2.zero);
                RawImage art = artObject.GetComponent<RawImage>();
                art.raycastTarget = false;
                AspectRatioFitter artAspect =
                    artObject.GetComponent<AspectRatioFitter>();
                artAspect.aspectMode =
                    AspectRatioFitter.AspectMode.HeightControlsWidth;
                artAspect.aspectRatio = 1f;

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
                Button npcActionTab = CreateButton(
                    "NpcActionTabButton",
                    npcTradePanel.transform,
                    font,
                    "行动",
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
                    "先开始人物互动，再选择行动。",
                    18f,
                    new Color(0.88f, 0.88f, 0.82f),
                    TextAlignmentOptions.TopLeft,
                    new Vector2(0.03f, 0.02f),
                    new Vector2(0.97f, 0.16f));
                npcTradeHint.enableWordWrapping = true;

                Button localMarketButton = CreateButton(
                    "LocalMarketButton",
                    screenCanvas,
                    font,
                    "市场",
                    new Color(0.08f, 0.46f, 0.52f, 0.96f),
                    new Vector2(0.245f, 0.025f),
                    new Vector2(0.385f, 0.082f),
                    25f);
                localMarketButton.gameObject.SetActive(false);

                GameObject publicMarketModal = CreateUiObject(
                    "PublicMarketModal",
                    screenCanvas,
                    typeof(CanvasRenderer),
                    typeof(Image));
                SetRect(
                    (RectTransform)publicMarketModal.transform,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero);
                publicMarketModal.GetComponent<Image>().color =
                    new Color(0.015f, 0.02f, 0.026f, 0.96f);
                publicMarketModal.transform.SetAsLastSibling();

                TMP_Text publicMarketTitle = CreateText(
                    "PublicMarketTitle",
                    publicMarketModal.transform,
                    font,
                    "地点 · 公共市场",
                    34f,
                    new Color(0.38f, 0.86f, 0.68f, 1f),
                    TextAlignmentOptions.MidlineLeft,
                    new Vector2(0.04f, 0.90f),
                    new Vector2(0.60f, 0.975f));
                TMP_Text publicMarketSummary = CreateText(
                    "PublicMarketSummary",
                    publicMarketModal.transform,
                    font,
                    "金币 0    市场资金 0    背包 0/0",
                    22f,
                    new Color(0.88f, 0.9f, 0.92f, 1f),
                    TextAlignmentOptions.MidlineLeft,
                    new Vector2(0.42f, 0.90f),
                    new Vector2(0.86f, 0.975f));
                Button publicMarketClose = CreateButton(
                    "PublicMarketCloseButton",
                    publicMarketModal.transform,
                    font,
                    "关闭",
                    new Color(0.34f, 0.36f, 0.40f, 1f),
                    new Vector2(0.88f, 0.91f),
                    new Vector2(0.97f, 0.97f),
                    22f);

                Button publicMarketBuyMode = CreateButton(
                    "PublicMarketBuyModeButton",
                    publicMarketModal.transform,
                    font,
                    "购买",
                    new Color(0.12f, 0.52f, 0.37f, 1f),
                    new Vector2(0.04f, 0.82f),
                    new Vector2(0.16f, 0.885f),
                    23f);
                Button publicMarketSellMode = CreateButton(
                    "PublicMarketSellModeButton",
                    publicMarketModal.transform,
                    font,
                    "出售",
                    new Color(0.55f, 0.37f, 0.13f, 1f),
                    new Vector2(0.17f, 0.82f),
                    new Vector2(0.29f, 0.885f),
                    23f);
                CreateText(
                    "PublicMarketModeHint",
                    publicMarketModal.transform,
                    font,
                    "选择交易方向，再从完整市场商品表中选择商品",
                    19f,
                    new Color(0.72f, 0.76f, 0.78f, 1f),
                    TextAlignmentOptions.MidlineLeft,
                    new Vector2(0.31f, 0.82f),
                    new Vector2(0.64f, 0.885f));
                CreateMarketColumnHeader(
                    publicMarketModal.transform,
                    font);

                GameObject publicMarketScrollObject = CreateUiObject(
                    "PublicMarketScrollView",
                    publicMarketModal.transform,
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(ScrollRect));
                SetRect(
                    (RectTransform)publicMarketScrollObject.transform,
                    new Vector2(0.04f, 0.08f),
                    new Vector2(0.65f, 0.755f),
                    Vector2.zero,
                    Vector2.zero);
                publicMarketScrollObject.GetComponent<Image>().color =
                    new Color(0f, 0f, 0f, 0.28f);

                GameObject publicMarketViewport = CreateUiObject(
                    "Viewport",
                    publicMarketScrollObject.transform,
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Mask));
                SetRect(
                    (RectTransform)publicMarketViewport.transform,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero);
                publicMarketViewport.GetComponent<Image>().color =
                    new Color(1f, 1f, 1f, 0.01f);
                publicMarketViewport.GetComponent<Mask>().showMaskGraphic =
                    false;

                GameObject publicMarketContent = CreateUiObject(
                    "PublicMarketListContent",
                    publicMarketViewport.transform,
                    typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter));
                RectTransform publicMarketListRoot =
                    (RectTransform)publicMarketContent.transform;
                SetRect(
                    publicMarketListRoot,
                    new Vector2(0f, 1f),
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero);
                publicMarketListRoot.pivot = new Vector2(0.5f, 1f);
                VerticalLayoutGroup publicMarketLayout =
                    publicMarketContent.GetComponent<VerticalLayoutGroup>();
                publicMarketLayout.spacing = 8f;
                publicMarketLayout.padding =
                    new RectOffset(10, 10, 10, 10);
                publicMarketLayout.childControlWidth = true;
                publicMarketLayout.childForceExpandWidth = true;
                publicMarketLayout.childControlHeight = true;
                publicMarketLayout.childForceExpandHeight = false;
                publicMarketContent.GetComponent<ContentSizeFitter>()
                    .verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                ScrollRect publicMarketScroll =
                    publicMarketScrollObject.GetComponent<ScrollRect>();
                publicMarketScroll.viewport =
                    (RectTransform)publicMarketViewport.transform;
                publicMarketScroll.content = publicMarketListRoot;
                publicMarketScroll.horizontal = false;
                publicMarketScroll.vertical = true;
                publicMarketScroll.movementType =
                    ScrollRect.MovementType.Clamped;

                MarketCommodityListItem publicMarketRowTemplate =
                    CreateMarketCommodityRowTemplate(
                        publicMarketContent.transform,
                        font);
                publicMarketRowTemplate.gameObject.SetActive(false);

                GameObject publicMarketSelectionPanel = CreateUiObject(
                    "PublicMarketSelectionPanel",
                    publicMarketModal.transform,
                    typeof(CanvasRenderer),
                    typeof(Image));
                SetRect(
                    (RectTransform)publicMarketSelectionPanel.transform,
                    new Vector2(0.67f, 0.08f),
                    new Vector2(0.97f, 0.80f),
                    Vector2.zero,
                    Vector2.zero);
                publicMarketSelectionPanel.GetComponent<Image>().color =
                    new Color(0.055f, 0.07f, 0.085f, 0.98f);

                TMP_Text publicMarketSelection = CreateText(
                    "PublicMarketSelectionLabel",
                    publicMarketSelectionPanel.transform,
                    font,
                    "请选择一种商品",
                    22f,
                    Color.white,
                    TextAlignmentOptions.TopLeft,
                    new Vector2(0.07f, 0.54f),
                    new Vector2(0.93f, 0.93f));
                publicMarketSelection.enableWordWrapping = true;
                TMP_Text publicMarketQuantity = CreateText(
                    "PublicMarketQuantityLabel",
                    publicMarketSelectionPanel.transform,
                    font,
                    "数量 —",
                    21f,
                    new Color(0.94f, 0.86f, 0.64f, 1f),
                    TextAlignmentOptions.TopLeft,
                    new Vector2(0.07f, 0.39f),
                    new Vector2(0.93f, 0.54f));
                publicMarketQuantity.enableWordWrapping = true;

                Button publicMarketDecrease = CreateButton(
                    "PublicMarketDecreaseButton",
                    publicMarketSelectionPanel.transform,
                    font,
                    "−",
                    new Color(0.25f, 0.31f, 0.36f, 1f),
                    new Vector2(0.07f, 0.28f),
                    new Vector2(0.25f, 0.37f),
                    28f);
                Button publicMarketIncrease = CreateButton(
                    "PublicMarketIncreaseButton",
                    publicMarketSelectionPanel.transform,
                    font,
                    "+",
                    new Color(0.25f, 0.31f, 0.36f, 1f),
                    new Vector2(0.28f, 0.28f),
                    new Vector2(0.46f, 0.37f),
                    28f);
                Button publicMarketMaximum = CreateButton(
                    "PublicMarketMaximumButton",
                    publicMarketSelectionPanel.transform,
                    font,
                    "最大",
                    new Color(0.31f, 0.38f, 0.46f, 1f),
                    new Vector2(0.50f, 0.28f),
                    new Vector2(0.93f, 0.37f),
                    21f);
                Button publicMarketConfirm = CreateButton(
                    "PublicMarketConfirmButton",
                    publicMarketSelectionPanel.transform,
                    font,
                    "确认购买",
                    new Color(0.12f, 0.55f, 0.39f, 1f),
                    new Vector2(0.07f, 0.13f),
                    new Vector2(0.93f, 0.24f),
                    24f);
                TMP_Text publicMarketHint = CreateText(
                    "PublicMarketHint",
                    publicMarketSelectionPanel.transform,
                    font,
                    "选择商品后设置交易数量。",
                    18f,
                    new Color(0.86f, 0.86f, 0.80f, 1f),
                    TextAlignmentOptions.TopLeft,
                    new Vector2(0.07f, 0.02f),
                    new Vector2(0.93f, 0.11f));
                publicMarketHint.enableWordWrapping = true;

                PublicMarketTradeScreen publicMarketScreen =
                    root.AddComponent<PublicMarketTradeScreen>();
                var serializedMarketScreen = new SerializedObject(
                    publicMarketScreen);
                SetReference(
                    serializedMarketScreen,
                    "marketButton",
                    localMarketButton);
                SetReference(
                    serializedMarketScreen,
                    "modalRoot",
                    publicMarketModal);
                SetReference(
                    serializedMarketScreen,
                    "closeButton",
                    publicMarketClose);
                SetReference(
                    serializedMarketScreen,
                    "titleLabel",
                    publicMarketTitle);
                SetReference(
                    serializedMarketScreen,
                    "summaryLabel",
                    publicMarketSummary);
                SetReference(
                    serializedMarketScreen,
                    "buyModeButton",
                    publicMarketBuyMode);
                SetReference(
                    serializedMarketScreen,
                    "sellModeButton",
                    publicMarketSellMode);
                SetReference(
                    serializedMarketScreen,
                    "marketListRoot",
                    publicMarketListRoot);
                SetReference(
                    serializedMarketScreen,
                    "marketRowTemplate",
                    publicMarketRowTemplate);
                SetReference(
                    serializedMarketScreen,
                    "selectionLabel",
                    publicMarketSelection);
                SetReference(
                    serializedMarketScreen,
                    "quantityLabel",
                    publicMarketQuantity);
                SetReference(
                    serializedMarketScreen,
                    "hintLabel",
                    publicMarketHint);
                SetReference(
                    serializedMarketScreen,
                    "decreaseButton",
                    publicMarketDecrease);
                SetReference(
                    serializedMarketScreen,
                    "increaseButton",
                    publicMarketIncrease);
                SetReference(
                    serializedMarketScreen,
                    "maximumButton",
                    publicMarketMaximum);
                SetReference(
                    serializedMarketScreen,
                    "confirmButton",
                    publicMarketConfirm);
                serializedMarketScreen.ApplyModifiedPropertiesWithoutUndo();
                publicMarketModal.SetActive(false);

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
                SetReference(
                    serializedView,
                    "npcActionTabButton",
                    npcActionTab);
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
                recipesToggle.gameObject.SetActive(false);
                if (recipesView != null)
                    recipesView.gameObject.SetActive(false);

                ReplacePublicMarketWithDualInventory(
                    root,
                    screenCanvas,
                    font,
                    localMarketButton);
                PrefabUtility.SaveAsPrefabAsset(root, UiRootPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Installed world-map location sidebar into the native UIRoot prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ReplacePublicMarketWithDualInventory(
            GameObject root,
            Transform screenCanvas,
            TMP_FontAsset font,
            Button marketButton)
        {
            Transform existingModal =
                FindDescendant(screenCanvas, "PublicMarketModal");
            if (existingModal != null)
                Object.DestroyImmediate(existingModal.gameObject);
            PublicMarketTradeScreen existingScreen =
                root.GetComponent<PublicMarketTradeScreen>();
            if (existingScreen != null)
                Object.DestroyImmediate(existingScreen);

            GameObject modal = CreateUiObject(
                "PublicMarketModal",
                screenCanvas,
                typeof(CanvasRenderer),
                typeof(Image));
            SetRect(
                (RectTransform)modal.transform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            modal.GetComponent<Image>().color =
                new Color(0.012f, 0.018f, 0.024f, 0.985f);
            modal.transform.SetAsLastSibling();

            TMP_Text title = CreateText(
                "PublicMarketTitle",
                modal.transform,
                font,
                "河湾市场",
                34f,
                new Color(0.38f, 0.86f, 0.68f, 1f),
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.03f, 0.905f),
                new Vector2(0.38f, 0.975f));
            TMP_Text summary = CreateText(
                "PublicMarketSummary",
                modal.transform,
                font,
                "金币 0    市场资金 0    背包 0/0",
                21f,
                new Color(0.88f, 0.9f, 0.92f, 1f),
                TextAlignmentOptions.Midline,
                new Vector2(0.33f, 0.905f),
                new Vector2(0.86f, 0.975f));
            Button close = CreateButton(
                "PublicMarketCloseButton",
                modal.transform,
                font,
                "关闭市场",
                new Color(0.30f, 0.33f, 0.38f, 1f),
                new Vector2(0.88f, 0.915f),
                new Vector2(0.97f, 0.97f),
                22f);

            MarketInventoryPanelParts backpack =
                CreateMarketInventoryPanel(
                    "PublicMarketBackpackPanel",
                    "我的背包",
                    "点击商品出售给当地市场",
                    "PublicMarketBackpackListContent",
                    "PublicMarketBackpackRowTemplate",
                    modal.transform,
                    font,
                    new Vector2(0.03f, 0.07f),
                    new Vector2(0.34f, 0.875f),
                    new Color(0.055f, 0.065f, 0.085f, 0.99f));

            GameObject transactionPanel = CreateUiObject(
                "PublicMarketTransactionPanel",
                modal.transform,
                typeof(CanvasRenderer),
                typeof(Image));
            SetRect(
                (RectTransform)transactionPanel.transform,
                new Vector2(0.355f, 0.07f),
                new Vector2(0.645f, 0.875f),
                Vector2.zero,
                Vector2.zero);
            transactionPanel.GetComponent<Image>().color =
                new Color(0.07f, 0.08f, 0.095f, 0.99f);
            CreateText(
                "PublicMarketTransactionTitle",
                transactionPanel.transform,
                font,
                "交易单",
                27f,
                new Color(0.94f, 0.85f, 0.62f, 1f),
                TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.90f),
                new Vector2(0.95f, 0.985f));
            CreateText(
                "PublicMarketTransactionGuide",
                transactionPanel.transform,
                font,
                "左侧出售　｜　右侧购买",
                17f,
                new Color(0.67f, 0.72f, 0.74f, 1f),
                TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.84f),
                new Vector2(0.95f, 0.90f));

            GameObject selectionCard = CreateUiObject(
                "PublicMarketSelectionCard",
                transactionPanel.transform,
                typeof(CanvasRenderer),
                typeof(Image));
            SetRect(
                (RectTransform)selectionCard.transform,
                new Vector2(0.055f, 0.39f),
                new Vector2(0.945f, 0.81f),
                Vector2.zero,
                Vector2.zero);
            selectionCard.GetComponent<Image>().color =
                new Color(1f, 1f, 1f, 0.46f);

            TMP_Text selection = CreateText(
                "PublicMarketSelectionLabel",
                selectionCard.transform,
                font,
                "请选择左侧商品出售\n或选择右侧商品购买",
                21f,
                Color.white,
                TextAlignmentOptions.TopLeft,
                new Vector2(0.06f, 0.40f),
                new Vector2(0.94f, 0.95f));
            selection.enableWordWrapping = true;
            TMP_Text quantity = CreateText(
                "PublicMarketQuantityLabel",
                selectionCard.transform,
                font,
                "数量 —\n交易总额 —",
                20f,
                new Color(0.94f, 0.86f, 0.64f, 1f),
                TextAlignmentOptions.TopLeft,
                new Vector2(0.06f, 0.05f),
                new Vector2(0.94f, 0.36f));
            quantity.enableWordWrapping = true;

            Button decrease = CreateButton(
                "PublicMarketDecreaseButton",
                transactionPanel.transform,
                font,
                "−",
                new Color(0.23f, 0.28f, 0.33f, 1f),
                new Vector2(0.07f, 0.25f),
                new Vector2(0.25f, 0.33f),
                27f);
            Button increase = CreateButton(
                "PublicMarketIncreaseButton",
                transactionPanel.transform,
                font,
                "+",
                new Color(0.23f, 0.28f, 0.33f, 1f),
                new Vector2(0.28f, 0.25f),
                new Vector2(0.46f, 0.33f),
                27f);
            Button maximum = CreateButton(
                "PublicMarketMaximumButton",
                transactionPanel.transform,
                font,
                "最大",
                new Color(0.30f, 0.36f, 0.43f, 1f),
                new Vector2(0.50f, 0.25f),
                new Vector2(0.93f, 0.33f),
                20f);
            Button confirm = CreateButton(
                "PublicMarketConfirmButton",
                transactionPanel.transform,
                font,
                "确认交易",
                new Color(0.12f, 0.55f, 0.39f, 1f),
                new Vector2(0.07f, 0.11f),
                new Vector2(0.93f, 0.21f),
                23f);
            TMP_Text hint = CreateText(
                "PublicMarketHint",
                transactionPanel.transform,
                font,
                "点击左侧背包出售，点击右侧市场购买。",
                17f,
                new Color(0.82f, 0.84f, 0.80f, 1f),
                TextAlignmentOptions.TopLeft,
                new Vector2(0.07f, 0.025f),
                new Vector2(0.93f, 0.09f));
            hint.enableWordWrapping = true;

            MarketInventoryPanelParts market =
                CreateMarketInventoryPanel(
                    "PublicMarketMarketPanel",
                    "河湾市场货物",
                    "点击商品从当地市场买入",
                    "PublicMarketMarketListContent",
                    "PublicMarketMarketRowTemplate",
                    modal.transform,
                    font,
                    new Vector2(0.66f, 0.07f),
                    new Vector2(0.97f, 0.875f),
                    new Color(0.045f, 0.08f, 0.075f, 0.99f));

            PublicMarketTradeScreen screen =
                root.AddComponent<PublicMarketTradeScreen>();
            var serialized = new SerializedObject(screen);
            SetReference(serialized, "marketButton", marketButton);
            SetReference(serialized, "modalRoot", modal);
            SetReference(serialized, "closeButton", close);
            SetReference(serialized, "titleLabel", title);
            SetReference(serialized, "summaryLabel", summary);
            SetReference(
                serialized,
                "backpackListRoot",
                backpack.ListRoot);
            SetReference(
                serialized,
                "backpackRowTemplate",
                backpack.RowTemplate);
            SetReference(
                serialized,
                "backpackEmptyState",
                backpack.EmptyState);
            SetReference(
                serialized,
                "marketListRoot",
                market.ListRoot);
            SetReference(
                serialized,
                "marketRowTemplate",
                market.RowTemplate);
            SetReference(
                serialized,
                "marketEmptyState",
                market.EmptyState);
            SetReference(serialized, "selectionLabel", selection);
            SetReference(serialized, "quantityLabel", quantity);
            SetReference(serialized, "hintLabel", hint);
            SetReference(serialized, "decreaseButton", decrease);
            SetReference(serialized, "increaseButton", increase);
            SetReference(serialized, "maximumButton", maximum);
            SetReference(serialized, "confirmButton", confirm);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            modal.SetActive(false);
            PublicMarketFantasySkinInstaller
                .ApplyToPrefabContents(root);
            CommonFantasyHudSkinInstaller
                .ApplyToPrefabContents(root);
        }

        private static void UpgradeExistingPublicMarket(
            GameObject root,
            Transform modal,
            TMP_FontAsset font,
            PublicMarketTradeScreen screen)
        {
            modal.SetAsLastSibling();
            TMP_Text closeLabel = FindDescendant(
                    modal,
                    "PublicMarketCloseButton")
                ?.GetComponentInChildren<TMP_Text>(true);
            if (closeLabel != null)
                closeLabel.text = "关闭市场";

            foreach (string rowName in new[]
                     {
                         "PublicMarketBackpackRowTemplate",
                         "PublicMarketMarketRowTemplate"
                     })
            {
                Transform row = FindDescendant(modal, rowName);
                if (row == null)
                    continue;
                row.gameObject.SetActive(false);
                LayoutElement layout = row.GetComponent<LayoutElement>();
                if (layout != null)
                    layout.preferredHeight = 122f;
                TMP_Text nameLabel = FindDescendant(row, "Name")
                    ?.GetComponent<TMP_Text>();
                if (nameLabel != null)
                    nameLabel.fontSize = 21f;
            }

            GameObject backpackEmpty = EnsureMarketEmptyState(
                modal,
                "PublicMarketBackpackPanelScrollView",
                "PublicMarketBackpackEmptyState",
                "背包中没有可出售的商品",
                font);
            GameObject marketEmpty = EnsureMarketEmptyState(
                modal,
                "PublicMarketMarketPanelScrollView",
                "PublicMarketMarketEmptyState",
                "市场暂时没有可购买的商品",
                font);

            Transform transaction = FindDescendant(
                modal,
                "PublicMarketTransactionPanel");
            Transform selectionCard = FindDescendant(
                transaction,
                "PublicMarketSelectionCard");
            if (transaction != null && selectionCard == null)
            {
                GameObject card = CreateUiObject(
                    "PublicMarketSelectionCard",
                    transaction,
                    typeof(CanvasRenderer),
                    typeof(Image));
                SetRect(
                    (RectTransform)card.transform,
                    new Vector2(0.055f, 0.39f),
                    new Vector2(0.945f, 0.81f),
                    Vector2.zero,
                    Vector2.zero);
                card.GetComponent<Image>().color =
                    new Color(1f, 1f, 1f, 0.46f);
                selectionCard = card.transform;
            }
            if (selectionCard != null)
            {
                SetRect(
                    (RectTransform)selectionCard,
                    new Vector2(0.055f, 0.39f),
                    new Vector2(0.945f, 0.81f),
                    Vector2.zero,
                    Vector2.zero);
            }
            TMP_Text selection = FindDescendant(
                    modal,
                    "PublicMarketSelectionLabel")
                ?.GetComponent<TMP_Text>();
            TMP_Text quantity = FindDescendant(
                    modal,
                    "PublicMarketQuantityLabel")
                ?.GetComponent<TMP_Text>();
            if (selectionCard != null && selection != null)
            {
                selection.transform.SetParent(selectionCard, false);
                selection.text = "请选择左侧商品出售\n或选择右侧商品购买";
                SetRect(
                    (RectTransform)selection.transform,
                    new Vector2(0.06f, 0.40f),
                    new Vector2(0.94f, 0.95f),
                    Vector2.zero,
                    Vector2.zero);
            }
            if (selectionCard != null && quantity != null)
            {
                quantity.transform.SetParent(selectionCard, false);
                quantity.text = "数量 —\n交易总额 —";
                SetRect(
                    (RectTransform)quantity.transform,
                    new Vector2(0.06f, 0.05f),
                    new Vector2(0.94f, 0.36f),
                    Vector2.zero,
                    Vector2.zero);
            }

            var serialized = new SerializedObject(screen);
            SetReference(
                serialized,
                "backpackEmptyState",
                backpackEmpty);
            SetReference(
                serialized,
                "marketEmptyState",
                marketEmpty);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PublicMarketFantasySkinInstaller.ApplyToPrefabContents(root);
            CommonFantasyHudSkinInstaller.ApplyToPrefabContents(root);
        }

        private static GameObject EnsureMarketEmptyState(
            Transform modal,
            string scrollName,
            string emptyStateName,
            string message,
            TMP_FontAsset font)
        {
            Transform existing = FindDescendant(modal, emptyStateName);
            if (existing != null)
                return existing.gameObject;
            Transform scroll = FindDescendant(modal, scrollName);
            if (scroll == null)
                return null;
            TMP_Text emptyState = CreateText(
                emptyStateName,
                scroll,
                font,
                message,
                19f,
                new Color(0.34f, 0.38f, 0.42f, 1f),
                TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.38f),
                new Vector2(0.92f, 0.62f));
            emptyState.raycastTarget = false;
            return emptyState.gameObject;
        }

        private static MarketInventoryPanelParts CreateMarketInventoryPanel(
            string panelName,
            string title,
            string subtitle,
            string contentName,
            string templateName,
            Transform parent,
            TMP_FontAsset font,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color backgroundColor)
        {
            GameObject panel = CreateUiObject(
                panelName,
                parent,
                typeof(CanvasRenderer),
                typeof(Image));
            SetRect(
                (RectTransform)panel.transform,
                anchorMin,
                anchorMax,
                Vector2.zero,
                Vector2.zero);
            panel.GetComponent<Image>().color = backgroundColor;

            CreateText(
                $"{panelName}Title",
                panel.transform,
                font,
                title,
                26f,
                Color.white,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.05f, 0.90f),
                new Vector2(0.95f, 0.98f));
            CreateText(
                $"{panelName}Subtitle",
                panel.transform,
                font,
                subtitle,
                17f,
                new Color(0.66f, 0.72f, 0.74f, 1f),
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.05f, 0.83f),
                new Vector2(0.95f, 0.90f));

            GameObject scrollObject = CreateUiObject(
                $"{panelName}ScrollView",
                panel.transform,
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(ScrollRect));
            SetRect(
                (RectTransform)scrollObject.transform,
                new Vector2(0.03f, 0.03f),
                new Vector2(0.97f, 0.81f),
                Vector2.zero,
                Vector2.zero);
            scrollObject.GetComponent<Image>().color =
                new Color(0f, 0f, 0f, 0.24f);

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
                contentName,
                viewport.transform,
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            RectTransform content =
                (RectTransform)contentObject.transform;
            SetRect(
                content,
                new Vector2(0f, 1f),
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            VerticalLayoutGroup layout =
                contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 7f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            contentObject.GetComponent<ContentSizeFitter>()
                .verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            MarketCommodityListItem row =
                CreateDualInventoryMarketRow(
                    templateName,
                    content,
                    font);
            row.gameObject.SetActive(false);
            bool isBackpack = panelName.Contains("Backpack");
            TMP_Text emptyState = CreateText(
                isBackpack
                    ? "PublicMarketBackpackEmptyState"
                    : "PublicMarketMarketEmptyState",
                scrollObject.transform,
                font,
                isBackpack
                    ? "背包中没有可出售的商品"
                    : "市场暂时没有可购买的商品",
                19f,
                new Color(0.34f, 0.38f, 0.42f, 1f),
                TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.38f),
                new Vector2(0.92f, 0.62f));
            emptyState.raycastTarget = false;
            return new MarketInventoryPanelParts(
                content,
                row,
                emptyState.gameObject);
        }

        private static MarketCommodityListItem CreateDualInventoryMarketRow(
            string name,
            Transform parent,
            TMP_FontAsset font)
        {
            GameObject rowObject = CreateUiObject(
                name,
                parent,
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement),
                typeof(MarketCommodityListItem));
            Image background = rowObject.GetComponent<Image>();
            background.color =
                new Color(0.09f, 0.11f, 0.13f, 0.98f);
            Button selectionButton = rowObject.GetComponent<Button>();
            selectionButton.targetGraphic = background;
            selectionButton.transition =
                Selectable.Transition.ColorTint;
            LayoutElement layout =
                rowObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 122f;
            layout.flexibleWidth = 1f;

            GameObject iconObject = CreateUiObject(
                "Icon",
                rowObject.transform,
                typeof(CanvasRenderer),
                typeof(RawImage),
                typeof(AspectRatioFitter));
            SetRect(
                (RectTransform)iconObject.transform,
                new Vector2(0.025f, 0.18f),
                new Vector2(0.19f, 0.82f),
                Vector2.zero,
                Vector2.zero);
            RawImage icon = iconObject.GetComponent<RawImage>();
            icon.raycastTarget = false;
            AspectRatioFitter aspect =
                iconObject.GetComponent<AspectRatioFitter>();
            aspect.aspectMode =
                AspectRatioFitter.AspectMode.HeightControlsWidth;
            aspect.aspectRatio = 1f;

            TMP_Text itemName = CreateText(
                "Name",
                rowObject.transform,
                font,
                "商品",
                21f,
                Color.white,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.21f, 0.70f),
                new Vector2(0.95f, 0.96f));
            TMP_Text price = CreateText(
                "Price",
                rowObject.transform,
                font,
                "买入价 0 金币/枚",
                17f,
                new Color(0.94f, 0.84f, 0.58f, 1f),
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.21f, 0.43f),
                new Vector2(0.95f, 0.71f));
            TMP_Text quantity = CreateText(
                "Quantity",
                rowObject.transform,
                font,
                "库存 0",
                15f,
                new Color(0.78f, 0.82f, 0.84f, 1f),
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.21f, 0.20f),
                new Vector2(0.61f, 0.44f));
            TMP_Text trend = CreateText(
                "Trend",
                rowObject.transform,
                font,
                "正常",
                15f,
                new Color(0.48f, 0.82f, 0.66f, 1f),
                TextAlignmentOptions.MidlineRight,
                new Vector2(0.61f, 0.20f),
                new Vector2(0.95f, 0.44f));
            TMP_Text details = CreateText(
                "Details",
                rowObject.transform,
                font,
                "你有 0 件",
                14f,
                new Color(0.63f, 0.68f, 0.70f, 1f),
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.21f, 0.02f),
                new Vector2(0.95f, 0.21f));

            var serialized = new SerializedObject(
                rowObject.GetComponent<MarketCommodityListItem>());
            SetReference(serialized, "icon", icon);
            SetReference(serialized, "nameLabel", itemName);
            SetReference(serialized, "priceLabel", price);
            SetReference(serialized, "quantityLabel", quantity);
            SetReference(serialized, "detailsLabel", details);
            SetReference(serialized, "trendLabel", trend);
            SetReference(
                serialized,
                "selectionButton",
                selectionButton);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return rowObject.GetComponent<MarketCommodityListItem>();
        }

        private readonly struct MarketInventoryPanelParts
        {
            public MarketInventoryPanelParts(
                RectTransform listRoot,
                MarketCommodityListItem rowTemplate,
                GameObject emptyState)
            {
                ListRoot = listRoot;
                RowTemplate = rowTemplate;
                EmptyState = emptyState;
            }

            public RectTransform ListRoot { get; }
            public MarketCommodityListItem RowTemplate { get; }
            public GameObject EmptyState { get; }
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
                typeof(RawImage),
                typeof(AspectRatioFitter));
            SetRect(
                (RectTransform)iconObject.transform,
                new Vector2(0.02f, 0.12f),
                new Vector2(0.18f, 0.88f),
                Vector2.zero,
                Vector2.zero);
            RawImage icon = iconObject.GetComponent<RawImage>();
            icon.raycastTarget = false;
            AspectRatioFitter iconAspect =
                iconObject.GetComponent<AspectRatioFitter>();
            iconAspect.aspectMode =
                AspectRatioFitter.AspectMode.WidthControlsHeight;
            iconAspect.aspectRatio = 1f;

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

        private static void CreateMarketColumnHeader(
            Transform parent,
            TMP_FontAsset font)
        {
            Color color = new Color(0.72f, 0.78f, 0.8f, 1f);
            CreateMarketHeaderLabel(
                "MarketNameHeader",
                parent,
                font,
                "商品",
                color,
                0.104f,
                0.202f);
            CreateMarketHeaderLabel(
                "MarketSalePriceHeader",
                parent,
                font,
                "售价",
                color,
                0.205f,
                0.263f);
            CreateMarketHeaderLabel(
                "MarketPurchasePriceHeader",
                parent,
                font,
                "收购",
                color,
                0.266f,
                0.324f);
            CreateMarketHeaderLabel(
                "MarketStockHeader",
                parent,
                font,
                "库存",
                color,
                0.327f,
                0.376f);
            CreateMarketHeaderLabel(
                "MarketOwnedHeader",
                parent,
                font,
                "持有",
                color,
                0.379f,
                0.427f);
            CreateMarketHeaderLabel(
                "MarketTrendHeader",
                parent,
                font,
                "行情",
                color,
                0.430f,
                0.491f);
            CreateMarketHeaderLabel(
                "MarketActionHeader",
                parent,
                font,
                "操作",
                color,
                0.504f,
                0.641f);
        }

        private static void CreateMarketHeaderLabel(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string text,
            Color color,
            float minX,
            float maxX)
        {
            CreateText(
                name,
                parent,
                font,
                text,
                17f,
                color,
                TextAlignmentOptions.Center,
                new Vector2(minX, 0.765f),
                new Vector2(maxX, 0.81f));
        }

        private static MarketCommodityListItem
            CreateMarketCommodityRowTemplate(
                Transform parent,
                TMP_FontAsset font)
        {
            GameObject rowObject = CreateUiObject(
                "PublicMarketRowTemplate",
                parent,
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement),
                typeof(MarketCommodityListItem));
            rowObject.GetComponent<Image>().color =
                new Color(0.10f, 0.125f, 0.145f, 0.98f);
            LayoutElement rowLayout =
                rowObject.GetComponent<LayoutElement>();
            rowLayout.preferredHeight = 82f;
            rowLayout.flexibleWidth = 1f;

            GameObject iconObject = CreateUiObject(
                "Icon",
                rowObject.transform,
                typeof(CanvasRenderer),
                typeof(RawImage),
                typeof(AspectRatioFitter));
            SetRect(
                (RectTransform)iconObject.transform,
                new Vector2(0.015f, 0.13f),
                new Vector2(0.095f, 0.87f),
                Vector2.zero,
                Vector2.zero);
            RawImage icon = iconObject.GetComponent<RawImage>();
            icon.raycastTarget = false;
            AspectRatioFitter iconAspect =
                iconObject.GetComponent<AspectRatioFitter>();
            iconAspect.aspectMode =
                AspectRatioFitter.AspectMode.HeightControlsWidth;
            iconAspect.aspectRatio = 1f;

            TMP_Text nameLabel = CreateText(
                "Name",
                rowObject.transform,
                font,
                "商品",
                18f,
                Color.white,
                TextAlignmentOptions.MidlineLeft,
                new Vector2(0.105f, 0.08f),
                new Vector2(0.265f, 0.92f));
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = 14f;
            nameLabel.fontSizeMax = 18f;

            TMP_Text salePriceLabel = CreateMarketValueLabel(
                "SalePrice",
                rowObject.transform,
                font,
                "0",
                0.27f,
                0.365f);
            TMP_Text purchasePriceLabel = CreateMarketValueLabel(
                "PurchasePrice",
                rowObject.transform,
                font,
                "0",
                0.37f,
                0.465f);
            TMP_Text stockLabel = CreateMarketValueLabel(
                "Stock",
                rowObject.transform,
                font,
                "0",
                0.47f,
                0.55f);
            TMP_Text ownedLabel = CreateMarketValueLabel(
                "Owned",
                rowObject.transform,
                font,
                "0",
                0.555f,
                0.635f);
            TMP_Text trendLabel = CreateMarketValueLabel(
                "Trend",
                rowObject.transform,
                font,
                "正常",
                0.64f,
                0.74f);

            Button actionButton = CreateButton(
                "ActionButton",
                rowObject.transform,
                font,
                "购买",
                new Color(0.14f, 0.53f, 0.38f, 1f),
                new Vector2(0.76f, 0.17f),
                new Vector2(0.985f, 0.83f),
                18f);

            var serializedRow = new SerializedObject(
                rowObject.GetComponent<MarketCommodityListItem>());
            SetReference(serializedRow, "icon", icon);
            SetReference(serializedRow, "nameLabel", nameLabel);
            SetReference(
                serializedRow,
                "salePriceLabel",
                salePriceLabel);
            SetReference(
                serializedRow,
                "purchasePriceLabel",
                purchasePriceLabel);
            SetReference(serializedRow, "stockLabel", stockLabel);
            SetReference(serializedRow, "ownedLabel", ownedLabel);
            SetReference(serializedRow, "trendLabel", trendLabel);
            SetReference(
                serializedRow,
                "actionButton",
                actionButton);
            SetReference(
                serializedRow,
                "actionButtonLabel",
                actionButton.GetComponentInChildren<TMP_Text>(true));
            serializedRow.ApplyModifiedPropertiesWithoutUndo();
            return rowObject.GetComponent<MarketCommodityListItem>();
        }

        private static TMP_Text CreateMarketValueLabel(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string text,
            float minX,
            float maxX)
        {
            return CreateText(
                name,
                parent,
                font,
                text,
                18f,
                new Color(0.90f, 0.91f, 0.92f, 1f),
                TextAlignmentOptions.Center,
                new Vector2(minX, 0.08f),
                new Vector2(maxX, 0.92f));
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
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
                return null;

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
