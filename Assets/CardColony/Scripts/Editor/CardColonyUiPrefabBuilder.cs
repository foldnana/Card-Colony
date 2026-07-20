using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using CardColony.UnityIntegration;
using CardColony.UnityIntegration.UI;

namespace CardColony.Editor
{
    public static class CardColonyUiPrefabBuilder
    {
        public const string PrefabFolder = "Assets/CardColony/Prefabs";
        public const string ItemCardPrefabPath = PrefabFolder + "/ItemCardView.prefab";
        public const string GameUiPrefabPath = PrefabFolder + "/GameUiRoot.prefab";
        private const string ChineseFontPath = "Assets/Resources/Fonts/SIMYOU SDF.asset";

        [MenuItem("Tools/Card Colony/重建概念图风格可玩界面")]
        public static void BuildForAutomation()
        {
            EnsureFolder("Assets/CardColony", "Prefabs");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontPath);
            ItemCardView itemCardPrefab = BuildItemCardPrefab(font);
            BuildGameUiPrefab(font, itemCardPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Card Colony concept-style playable UI rebuilt.");
        }

        private static ItemCardView BuildItemCardPrefab(TMP_FontAsset font)
        {
            GameObject root = CreateUiObject("ItemCardView", null);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(168f, 220f);
            Image background = root.AddComponent<Image>();
            background.color = new Color(0.87f, 0.85f, 0.78f, 1f);
            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.07f, 0.08f, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);
            LayoutElement layout = root.AddComponent<LayoutElement>();
            layout.preferredWidth = 168f;
            layout.minWidth = 168f;
            layout.preferredHeight = 220f;
            layout.minHeight = 220f;

            Image header = CreatePanel("Header", root.transform, new Color(0.22f, 0.48f, 0.30f, 1f));
            SetRect(header.rectTransform, new Vector2(0f, 0.78f), Vector2.one, Vector2.zero, Vector2.zero);
            TMP_Text title = CreateText("Title", header.transform, font, 21f, FontStyles.Bold, Color.white);
            SetRect(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 2f), new Vector2(-6f, -2f));
            title.alignment = TextAlignmentOptions.Center;

            TMP_Text art = CreateText("CardArt", root.transform, font, 54f, FontStyles.Bold, new Color(0.15f, 0.18f, 0.17f, 1f));
            art.text = "物";
            SetRect(art.rectTransform, new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.77f), Vector2.zero, Vector2.zero);
            art.alignment = TextAlignmentOptions.Center;

            TMP_Text details = CreateText("Details", root.transform, font, 13f, FontStyles.Normal, new Color(0.16f, 0.18f, 0.18f, 1f));
            SetRect(details.rectTransform, new Vector2(0.06f, 0.24f), new Vector2(0.94f, 0.42f), Vector2.zero, Vector2.zero);
            details.alignment = TextAlignmentOptions.Center;
            details.enableWordWrapping = true;

            Button take = CreateButton("TakeButton", root.transform, font, "取出", 14f);
            SetRect((RectTransform)take.transform, new Vector2(0.05f, 0.04f), new Vector2(0.48f, 0.21f), Vector2.zero, Vector2.zero);
            Button split = CreateButton("SplitButton", root.transform, font, "拆分 1", 14f);
            SetRect((RectTransform)split.transform, new Vector2(0.52f, 0.04f), new Vector2(0.95f, 0.21f), Vector2.zero, Vector2.zero);

            ItemCardView view = root.AddComponent<ItemCardView>();
            SerializedObject serialized = new SerializedObject(view);
            SetReference(serialized, "titleText", title);
            SetReference(serialized, "detailsText", details);
            SetReference(serialized, "takeButton", take);
            SetReference(serialized, "splitButton", split);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ItemCardPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<ItemCardView>();
        }

        private static void BuildGameUiPrefab(
            TMP_FontAsset font,
            ItemCardView itemCardPrefab)
        {
            GameObject root = new GameObject("GameUiRoot");
            root.transform.localScale = Vector3.one;
            GameObject canvasRoot = CreateUiObject("CanvasRoot", root.transform);
            canvasRoot.transform.localScale = Vector3.one;
            Canvas canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRoot.AddComponent<GraphicRaycaster>();
            WorldClockDriver driver = root.AddComponent<WorldClockDriver>();

            Image worldView = CreatePanel("WorldMapView", canvasRoot.transform, Color.clear);
            SetRect(worldView.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            worldView.raycastTarget = false;
            Image forestView = CreatePanel("ForestView", canvasRoot.transform, Color.clear);
            SetRect(forestView.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            forestView.raycastTarget = false;

            // The center belongs to StackCraft's native 3D card board. These hidden
            // bindings keep the existing presenter/save compatibility without placing
            // button-shaped cards over the PhysicsRaycaster interaction surface.
            GameObject legacyBindings = CreateUiObject("LegacyActionBindings", canvasRoot.transform);
            Button explore = CreateButton("ExploreActionBinding", legacyBindings.transform, font, string.Empty, 1f);
            Button gather = CreateButton("GatherActionBinding", legacyBindings.transform, font, string.Empty, 1f);
            Button brew = CreateButton("BrewActionBinding", legacyBindings.transform, font, string.Empty, 1f);
            Button mapButton = CreateButton("MapViewButton", legacyBindings.transform, font, string.Empty, 1f);
            Button forestButton = CreateButton("ForestViewButton", legacyBindings.transform, font, string.Empty, 1f);
            GameObject playerBinding = CreateUiObject("PlayerCardBinding", legacyBindings.transform);
            RectTransform playerCard = (RectTransform)playerBinding.transform;
            legacyBindings.SetActive(false);

            Image topBar = CreatePanel("TopBar", canvasRoot.transform, new Color(0.04f, 0.055f, 0.065f, 0.93f));
            SetRect(topBar.rectTransform, new Vector2(0.01f, 0.925f), new Vector2(0.155f, 0.985f), Vector2.zero, Vector2.zero);
            TMP_Text timeText = CreateText("TimeText", topBar.transform, font, 23f, FontStyles.Bold, Color.white);
            SetRect(timeText.rectTransform, new Vector2(0.04f, 0f), new Vector2(0.72f, 1f), Vector2.zero, Vector2.zero);
            timeText.alignment = TextAlignmentOptions.MidlineLeft;
            Button pause = CreateButton("PauseButton", topBar.transform, font, "▶", 22f);
            SetRect((RectTransform)pause.transform, new Vector2(0.76f, 0.10f), new Vector2(0.97f, 0.90f), Vector2.zero, Vector2.zero);

            Image statsPanel = CreatePanel("TopStatsPanel", canvasRoot.transform, new Color(0.04f, 0.055f, 0.065f, 0.93f));
            SetRect(statsPanel.rectTransform, new Vector2(0.162f, 0.925f), new Vector2(0.345f, 0.985f), Vector2.zero, Vector2.zero);
            TMP_Text overviewStats = CreateText("OverviewStats", statsPanel.transform, font, 20f, FontStyles.Bold, Color.white);
            overviewStats.text = "♥ 15/15    ⚡ 4/4    ▣ 4/24";
            overviewStats.alignment = TextAlignmentOptions.Center;
            SetRect(overviewStats.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

            Image timeControls = CreatePanel("TimeControlPanel", canvasRoot.transform, new Color(0.04f, 0.055f, 0.065f, 0.91f));
            SetRect(timeControls.rectTransform, new Vector2(0.355f, 0.925f), new Vector2(0.635f, 0.985f), Vector2.zero, Vector2.zero);
            Button normal = CreateButton("NormalSpeedButton", timeControls.transform, font, "正常", 16f);
            SetRect((RectTransform)normal.transform, new Vector2(0.01f, 0.10f), new Vector2(0.30f, 0.90f), Vector2.zero, Vector2.zero);
            Button fast = CreateButton("FastSpeedButton", timeControls.transform, font, "四倍", 16f);
            SetRect((RectTransform)fast.transform, new Vector2(0.315f, 0.10f), new Vector2(0.605f, 0.90f), Vector2.zero, Vector2.zero);
            Button waiting = CreateButton("WaitingButton", timeControls.transform, font, "主动等待", 16f);
            SetRect((RectTransform)waiting.transform, new Vector2(0.62f, 0.10f), new Vector2(0.99f, 0.90f), Vector2.zero, Vector2.zero);

            TMP_Text statusText = CreateText("StatusText", canvasRoot.transform, font, 16f, FontStyles.Normal, new Color(0.92f, 0.92f, 0.78f));
            SetRect(statusText.rectTransform, new Vector2(0.355f, 0.895f), new Vector2(0.635f, 0.923f), Vector2.zero, Vector2.zero);
            statusText.alignment = TextAlignmentOptions.Center;

            Image progressPanel = CreatePanel("ActionProgress", canvasRoot.transform, new Color(0.03f, 0.045f, 0.05f, 0.90f));
            SetRect(progressPanel.rectTransform, new Vector2(0.355f, 0.855f), new Vector2(0.635f, 0.89f), Vector2.zero, Vector2.zero);
            Image progressFill = CreatePanel("Fill", progressPanel.transform, new Color(0.16f, 0.64f, 0.80f, 0.90f));
            SetRect(progressFill.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = 0;
            TMP_Text actionProgress = CreateText("ActionProgressText", progressPanel.transform, font, 18f, FontStyles.Bold, Color.white);
            SetRect(actionProgress.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            actionProgress.alignment = TextAlignmentOptions.Center;

            Image playerPanel = BuildPlayerAndBackpackPanel(canvasRoot.transform, font, out TMP_Text summary, out TMP_Text held, out RectTransform inventoryContent, out Button putBack);

            Image journal = CreatePanel("QuestRecipePanel", canvasRoot.transform, new Color(0.035f, 0.045f, 0.052f, 0.94f));
            SetRect(journal.rectTransform, new Vector2(0.805f, 0.035f), new Vector2(0.99f, 0.985f), Vector2.zero, Vector2.zero);
            Button questTab = CreateButton("QuestTabButton", journal.transform, font, "任务", 20f);
            SetRect((RectTransform)questTab.transform, new Vector2(0f, 0.91f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            Button recipeTab = CreateButton("RecipeTabButton", journal.transform, font, "配方", 20f);
            SetRect((RectTransform)recipeTab.transform, new Vector2(0.5f, 0.91f), Vector2.one, Vector2.zero, Vector2.zero);

            GameObject questContent = CreateUiObject("QuestContent", journal.transform);
            SetRect((RectTransform)questContent.transform, new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.90f), Vector2.zero, Vector2.zero);
            TMP_Text quests = CreateText("QuestList", questContent.transform, font, 19f, FontStyles.Normal, Color.white);
            quests.text = "前往低语森林\n\n采集三份草药\n\n制作治疗药水";
            quests.enableWordWrapping = true;
            quests.alignment = TextAlignmentOptions.TopLeft;
            SetRect(quests.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 12f), new Vector2(-8f, -12f));

            GameObject recipeContent = CreateUiObject("RecipeContent", journal.transform);
            SetRect((RectTransform)recipeContent.transform, new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.90f), Vector2.zero, Vector2.zero);
            Button recipeCard = CreateCard("HealingPotionRecipeCard", recipeContent.transform, font, "治疗药水", "药", "2草药\n15分钟制作", new Color(0.45f, 0.27f, 0.56f), new Vector2(0.5f, 0.67f), new Vector2(210f, 250f));
            recipeCard.interactable = false;
            TMP_Text recipeHint = CreateText("RecipeHint", recipeContent.transform, font, 16f, FontStyles.Normal, new Color(0.83f, 0.84f, 0.78f));
            recipeHint.text = "配方仅用于查看。\n请把材料与角色卡拖到一起，\n由卡堆触发制作。";
            recipeHint.enableWordWrapping = true;
            recipeHint.alignment = TextAlignmentOptions.Top;
            SetRect(recipeHint.rectTransform, new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.43f), Vector2.zero, Vector2.zero);
            recipeContent.SetActive(false);

            Button save = CreateButton("SaveButton", journal.transform, font, "保存", 16f);
            SetRect((RectTransform)save.transform, new Vector2(0.05f, 0.035f), new Vector2(0.46f, 0.105f), Vector2.zero, Vector2.zero);
            Button load = CreateButton("LoadButton", journal.transform, font, "读取", 16f);
            SetRect((RectTransform)load.transform, new Vector2(0.54f, 0.035f), new Vector2(0.95f, 0.105f), Vector2.zero, Vector2.zero);
            save.gameObject.SetActive(false);
            load.gameObject.SetActive(false);

            Image nav = CreatePanel("ViewNavigation", canvasRoot.transform, new Color(0.03f, 0.045f, 0.052f, 0.90f));
            SetRect(nav.rectTransform, new Vector2(0.355f, 0.02f), new Vector2(0.655f, 0.085f), Vector2.zero, Vector2.zero);
            Button interactTab = CreateButton("InteractTabButton", nav.transform, font, "互动", 18f);
            SetRect((RectTransform)interactTab.transform, new Vector2(0f, 0f), new Vector2(0.33f, 1f), Vector2.zero, Vector2.zero);
            Button campTab = CreateButton("CampTabButton", nav.transform, font, "扎营", 18f);
            SetRect((RectTransform)campTab.transform, new Vector2(0.335f, 0f), new Vector2(0.665f, 1f), Vector2.zero, Vector2.zero);
            campTab.interactable = false;
            Button mapTab = CreateButton("MapTabButton", nav.transform, font, "地图", 18f);
            SetRect((RectTransform)mapTab.transform, new Vector2(0.67f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
            mapTab.interactable = false;
            TMP_Text nativeInteractionHint = CreateText("NativeInteractionHint", nav.transform, font, 18f, FontStyles.Bold, Color.white);
            nativeInteractionHint.text = "拖动卡牌并叠放以互动";
            nativeInteractionHint.alignment = TextAlignmentOptions.Center;
            SetRect(nativeInteractionHint.rectTransform, new Vector2(0f, 1.05f), new Vector2(1f, 1.45f), Vector2.zero, Vector2.zero);
            nativeInteractionHint.raycastTarget = false;

            forestView.gameObject.SetActive(false);

            GameUiPresenter presenter = root.AddComponent<GameUiPresenter>();
            SerializedObject serialized = new SerializedObject(presenter);
            SetReference(serialized, "driver", driver);
            SetReference(serialized, "timeText", timeText);
            SetReference(serialized, "statusText", statusText);
            SetReference(serialized, "actionProgressText", actionProgress);
            SetReference(serialized, "pauseButton", pause);
            SetReference(serialized, "normalSpeedButton", normal);
            SetReference(serialized, "fastSpeedButton", fast);
            SetReference(serialized, "waitingButton", waiting);
            SetReference(serialized, "worldMapView", worldView.gameObject);
            SetReference(serialized, "forestView", forestView.gameObject);
            SetReference(serialized, "playerCard", playerCard);
            SetReference(serialized, "actionProgressFill", progressFill);
            SetReference(serialized, "mapViewButton", mapButton);
            SetReference(serialized, "forestViewButton", forestButton);
            SetReference(serialized, "questContent", questContent);
            SetReference(serialized, "recipeContent", recipeContent);
            SetReference(serialized, "questTabButton", questTab);
            SetReference(serialized, "recipeTabButton", recipeTab);
            SetReference(serialized, "exploreButton", explore);
            SetReference(serialized, "gatherButton", gather);
            SetReference(serialized, "brewButton", brew);
            SetReference(serialized, "saveButton", save);
            SetReference(serialized, "loadButton", load);
            SetReference(serialized, "inventorySummaryText", summary);
            SetReference(serialized, "heldCardText", held);
            SetReference(serialized, "inventoryContent", inventoryContent);
            SetReference(serialized, "itemCardPrefab", itemCardPrefab);
            SetReference(serialized, "putBackButton", putBack);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
            root.transform.localScale = Vector3.one;
            canvasRoot.transform.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(root, GameUiPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static Image BuildPlayerAndBackpackPanel(
            Transform parent,
            TMP_FontAsset font,
            out TMP_Text summary,
            out TMP_Text held,
            out RectTransform content,
            out Button putBack)
        {
            Image panel = CreatePanel("PlayerBackpackPanel", parent, new Color(0.035f, 0.045f, 0.052f, 0.94f));
            SetRect(panel.rectTransform, new Vector2(0.01f, 0.035f), new Vector2(0.22f, 0.355f), Vector2.zero, Vector2.zero);
            TMP_Text playerTitle = CreateText("PlayerTitle", panel.transform, font, 23f, FontStyles.Bold, new Color(0.28f, 0.72f, 0.88f));
            playerTitle.text = "旅行者";
            SetRect(playerTitle.rectTransform, new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.97f), Vector2.zero, Vector2.zero);
            playerTitle.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_Text playerStats = CreateText("PlayerStats", panel.transform, font, 17f, FontStyles.Normal, Color.white);
            playerStats.text = "生命15/15\n精力4/4";
            playerStats.enableWordWrapping = true;
            SetRect(playerStats.rectTransform, new Vector2(0.04f, 0.75f), new Vector2(0.96f, 0.86f), Vector2.zero, Vector2.zero);
            playerStats.alignment = TextAlignmentOptions.MidlineLeft;

            summary = CreateText("InventorySummaryText", panel.transform, font, 17f, FontStyles.Bold, Color.white);
            SetRect(summary.rectTransform, new Vector2(0.04f, 0.64f), new Vector2(0.96f, 0.75f), Vector2.zero, Vector2.zero);
            summary.alignment = TextAlignmentOptions.MidlineLeft;
            held = CreateText("HeldCardText", panel.transform, font, 15f, FontStyles.Normal, new Color(0.85f, 0.86f, 0.76f));
            SetRect(held.rectTransform, new Vector2(0.04f, 0.54f), new Vector2(0.68f, 0.64f), Vector2.zero, Vector2.zero);
            held.alignment = TextAlignmentOptions.MidlineLeft;
            putBack = CreateButton("PutBackButton", panel.transform, font, "放回", 14f);
            SetRect((RectTransform)putBack.transform, new Vector2(0.70f, 0.55f), new Vector2(0.96f, 0.63f), Vector2.zero, Vector2.zero);

            Image scrollBackground = CreatePanel("InventoryScroll", panel.transform, new Color(0.02f, 0.028f, 0.032f, 0.88f));
            SetRect(scrollBackground.rectTransform, new Vector2(0.035f, 0.035f), new Vector2(0.965f, 0.52f), Vector2.zero, Vector2.zero);
            for (int index = 0; index < 4; index++)
            {
                float left = 0.025f + index * 0.245f;
                Image slot = CreatePanel(
                    $"BackpackSlot{index + 1}",
                    scrollBackground.transform,
                    new Color(0.08f, 0.10f, 0.105f, 0.72f));
                SetRect(
                    slot.rectTransform,
                    new Vector2(left, 0.08f),
                    new Vector2(left + 0.215f, 0.92f),
                    Vector2.zero,
                    Vector2.zero);
                Outline slotOutline = slot.gameObject.AddComponent<Outline>();
                slotOutline.effectColor = new Color(0.55f, 0.58f, 0.56f, 0.75f);
                slotOutline.effectDistance = new Vector2(2f, -2f);
                slot.raycastTarget = false;
            }
            ScrollRect scroll = scrollBackground.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            GameObject viewportObject = CreateUiObject("Viewport", scrollBackground.transform);
            RectTransform viewport = viewportObject.GetComponent<RectTransform>();
            SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            viewportObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewportObject.AddComponent<RectMask2D>();
            GameObject contentObject = CreateUiObject("Content", viewport);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            HorizontalLayoutGroup layout = contentObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return panel;
        }

        private static Button CreateCard(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string title,
            string symbol,
            string footer,
            Color headerColor,
            Vector2 anchor,
            Vector2? size = null)
        {
            GameObject root = CreateUiObject(name, parent);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size ?? new Vector2(176f, 208f);
            Image background = root.AddComponent<Image>();
            background.color = new Color(0.88f, 0.86f, 0.79f, 0.98f);
            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(0.03f, 0.04f, 0.05f, 0.75f);
            outline.effectDistance = new Vector2(5f, -5f);
            Button button = root.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.82f, 0.94f, 1f, 1f);
            colors.pressedColor = new Color(0.68f, 0.82f, 0.90f, 1f);
            colors.disabledColor = new Color(0.72f, 0.72f, 0.70f, 0.92f);
            button.colors = colors;

            Image header = CreatePanel("Header", root.transform, headerColor);
            SetRect(header.rectTransform, new Vector2(0f, 0.80f), Vector2.one, Vector2.zero, Vector2.zero);
            TMP_Text titleText = CreateText("Title", header.transform, font, 19f, FontStyles.Bold, Color.white);
            titleText.text = title;
            titleText.alignment = TextAlignmentOptions.Center;
            SetRect(titleText.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));
            TMP_Text symbolText = CreateText("Symbol", root.transform, font, 57f, FontStyles.Bold, new Color(0.14f, 0.17f, 0.17f, 1f));
            symbolText.text = symbol;
            symbolText.alignment = TextAlignmentOptions.Center;
            SetRect(symbolText.rectTransform, new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.79f), Vector2.zero, Vector2.zero);
            TMP_Text footerText = CreateText("Footer", root.transform, font, 14f, FontStyles.Normal, new Color(0.16f, 0.18f, 0.18f, 1f));
            footerText.text = footer;
            footerText.alignment = TextAlignmentOptions.Center;
            footerText.enableWordWrapping = true;
            footerText.enableAutoSizing = true;
            footerText.fontSizeMin = 10f;
            footerText.fontSizeMax = 14f;
            SetRect(footerText.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.28f), Vector2.zero, Vector2.zero);
            return button;
        }

        private static void CreateRoute(Transform parent, Vector2 start, Vector2 end)
        {
            Image route = CreatePanel("TravelRoute", parent, new Color(0.18f, 0.72f, 0.84f, 0.75f));
            RectTransform rect = route.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            Vector2 delta = end - start;
            rect.sizeDelta = new Vector2(delta.magnitude, 6f);
            rect.anchoredPosition = (start + end) * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            route.raycastTarget = false;
        }

        private static Button CreateButton(string name, Transform parent, TMP_FontAsset font, string label, float fontSize)
        {
            GameObject gameObject = CreateUiObject(name, parent);
            Image image = gameObject.AddComponent<Image>();
            image.color = new Color(0.10f, 0.38f, 0.53f, 0.98f);
            Button button = gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.18f, 0.60f, 0.76f, 1f);
            colors.pressedColor = new Color(0.07f, 0.27f, 0.38f, 1f);
            colors.disabledColor = new Color(0.14f, 0.16f, 0.17f, 0.76f);
            button.colors = colors;
            TMP_Text text = CreateText("Label", gameObject.transform, font, fontSize, FontStyles.Normal, Color.white);
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            return button;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static Image CreatePanel(string name, Transform parent, Color color)
        {
            GameObject gameObject = CreateUiObject(name, parent);
            Image image = gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, float fontSize, FontStyles style, Color color)
        {
            GameObject gameObject = CreateUiObject(name, parent);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetReference(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new System.MissingFieldException(serialized.targetObject.GetType().Name, propertyName);
            property.objectReferenceValue = value;
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(fullPath))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
