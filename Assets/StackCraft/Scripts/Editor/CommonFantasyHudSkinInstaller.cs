using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CryingSnow.StackCraft.EditorTools
{
    public static class CommonFantasyHudSkinInstaller
    {
        private const string UiRootPath =
            "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";
        private const string DialoguePanelPath =
            "Assets/StackCraft/Prefabs/UI/DialoguePanel.prefab";
        private const string LocationScenePath =
            "Assets/StackCraft/Scenes/Location.unity";
        private const string UltimateShapeRoot =
            "Assets/UltimateCleanGUIPack/Common/Sprites/Shapes/";
        private const string PanelSpritePath =
            UltimateShapeRoot +
            "Semi Rounded/Semi Rounded - 300ppu.png";
        private const string BorderSpritePath =
            UltimateShapeRoot +
            "Semi Rounded/" +
            "Semi Rounded - Outline - 6px - 300ppu.png";
        private const string ListSpritePath = PanelSpritePath;
        private const string DarkButtonSpritePath = PanelSpritePath;
        private const string BlueButtonSpritePath = PanelSpritePath;
        private const string PurpleButtonSpritePath = BlueButtonSpritePath;
        private const string BrownButtonSpritePath = BlueButtonSpritePath;
        private const string GreenButtonSpritePath = BlueButtonSpritePath;
        private const string RedButtonSpritePath = BlueButtonSpritePath;
        private const string YellowButtonSpritePath = BlueButtonSpritePath;
        private const string SelectionLightSpritePath = BorderSpritePath;

        private static readonly Color PanelNavy =
            new(0.60f, 0.68f, 0.75f, 0.985f);
        private static readonly Color HeaderNavy =
            new(0.52f, 0.61f, 0.70f, 1f);
        private static readonly Color LightButtonBlue =
            new(0.52f, 0.64f, 0.76f, 1f);
        private static readonly Color LightDanger =
            new(0.74f, 0.52f, 0.50f, 1f);
        private static readonly Color LightSuccess =
            new(0.48f, 0.68f, 0.54f, 1f);
        private static readonly Color LightAmber =
            new(0.78f, 0.64f, 0.39f, 1f);
        private static readonly Color ContentNavy =
            new(0.67f, 0.72f, 0.77f, 1f);
        private static readonly Color ListNavy =
            new(0.61f, 0.67f, 0.72f, 1f);
        private static readonly Color MarketModalLight =
            new(0.73f, 0.77f, 0.80f, 1f);
        private static readonly Color MarketBackpackLight =
            new(0.55f, 0.68f, 0.79f, 1f);
        private static readonly Color MarketTransactionLight =
            new(0.75f, 0.62f, 0.43f, 1f);
        private static readonly Color MarketInventoryLight =
            new(0.55f, 0.72f, 0.62f, 1f);
        private static readonly Color QuestNavy =
            new(0.65f, 0.72f, 0.81f, 1f);
        private static readonly Color RecipeBrown =
            new(0.78f, 0.67f, 0.53f, 1f);
        private static readonly Color Cyan =
            new(0.10f, 0.38f, 0.58f, 1f);
        private static readonly Color JournalHeaderInk =
            new(0.05f, 0.24f, 0.31f, 1f);
        private static readonly Color WarmIvory =
            new(0.14f, 0.16f, 0.19f, 1f);
        private static readonly Color Gold =
            new(0.60f, 0.36f, 0.08f, 1f);
        private static readonly Color Green =
            new(0.15f, 0.44f, 0.25f, 1f);
        private static readonly Color Silver =
            new(0.34f, 0.38f, 0.42f, 1f);

        [MenuItem("Tools/StackCraft/Apply Ultimate Clean Light HUD Skin")]
        public static void Apply()
        {
            ApplyPrefabOnly();
            ApplyLocationScene();
        }

        [MenuItem("Tools/StackCraft/Apply Ultimate Clean HUD Prefab")]
        public static void ApplyPrefabOnly()
        {
            GameObject root =
                PrefabUtility.LoadPrefabContents(UiRootPath);
            try
            {
                ApplyToPrefabContents(root);
                PrefabUtility.SaveAsPrefabAsset(root, UiRootPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            GameObject dialogue =
                PrefabUtility.LoadPrefabContents(DialoguePanelPath);
            try
            {
                ApplyToDialoguePrefabContents(dialogue);
                PrefabUtility.SaveAsPrefabAsset(
                    dialogue,
                    DialoguePanelPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(dialogue);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Applied the Ultimate Clean Light HUD skin.");
        }

        [MenuItem(
            "Tools/StackCraft/Apply Location Return Button Fantasy Skin")]
        public static void ApplyLocationScene()
        {
            Scene scene = SceneManager.GetSceneByPath(LocationScenePath);
            bool openedByInstaller = !scene.IsValid() || !scene.isLoaded;
            if (openedByInstaller)
            {
                scene = EditorSceneManager.OpenScene(
                    LocationScenePath,
                    OpenSceneMode.Additive);
            }

            bool mayCloseScene = false;
            try
            {
                mayCloseScene =
                    ApplyLocationScene(scene, openedByInstaller);
            }
            finally
            {
                if (openedByInstaller &&
                    mayCloseScene &&
                    scene.IsValid() &&
                    scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        internal static bool ApplyLocationScene(
            Scene scene,
            bool openedByInstaller)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new MissingReferenceException(
                    "Location scene must be valid and loaded.");

            Transform returnTransform = FindDescendant(
                scene,
                "ReturnToWorldMapButton");
            if (returnTransform == null)
                throw new MissingReferenceException(
                    "Location scene return button is missing.");
            StyleLocationReturnButton(
                returnTransform.GetComponent<Button>());

            if (openedByInstaller)
            {
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new UnityException(
                        "Failed to save the location scene after applying " +
                        "the fantasy skin. The scene remains open and dirty.");
                }
                Debug.Log(
                    "Applied the fantasy skin to the location return button.");
                return true;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                "Applied the fantasy skin to the open location scene. " +
                "The scene remains open and unsaved.");
            return false;
        }

        internal static void ApplyToPrefabContents(GameObject root)
        {
            if (root == null)
                throw new MissingReferenceException(
                    "Common HUD skin requires a UIRoot prefab.");

            Sprite panelSprite = LoadRequiredSprite(PanelSpritePath);
            Sprite borderSprite = LoadRequiredSprite(BorderSpritePath);
            Sprite listSprite = LoadRequiredSprite(ListSpritePath);
            Sprite darkButton =
                LoadRequiredSprite(DarkButtonSpritePath);
            Sprite blueButton =
                LoadRequiredSprite(BlueButtonSpritePath);
            Sprite purpleButton =
                LoadRequiredSprite(PurpleButtonSpritePath);
            Sprite brownButton =
                LoadRequiredSprite(BrownButtonSpritePath);
            Sprite greenButton =
                LoadRequiredSprite(GreenButtonSpritePath);
            Sprite redButton =
                LoadRequiredSprite(RedButtonSpritePath);
            Sprite yellowButton =
                LoadRequiredSprite(YellowButtonSpritePath);
            Sprite selectionLight =
                LoadRequiredSprite(SelectionLightSpritePath);

            StyleTopStatus(root.transform, darkButton);
            StyleCommonSidebar(
                root.transform,
                panelSprite,
                blueButton);
            StyleLocationSidebar(
                root.transform,
                panelSprite,
                borderSprite,
                blueButton,
                purpleButton,
                brownButton,
                greenButton,
                selectionLight);
            StyleBackpack(
                root.transform,
                panelSprite,
                borderSprite,
                purpleButton,
                redButton,
                yellowButton);
            StyleInfoPanels(
                root.transform,
                panelSprite,
                borderSprite,
                listSprite,
                greenButton,
                brownButton,
                blueButton);
            StylePartyAndJournalPanels(
                root.transform,
                panelSprite);
            WireCombatLocationView(root.transform);
            StyleExtendedLightInterfaces(
                root.transform,
                panelSprite,
                borderSprite,
                listSprite);
            EnsureBoldText(root.transform);
        }

        private static void WireCombatLocationView(Transform root)
        {
            Transform combatPage = FindDescendant(root, "CombatHudPanel");
            Transform sidebar = FindDescendant(root, "MenuPanel");
            if (combatPage != null && sidebar != null &&
                combatPage.parent != sidebar)
            {
                combatPage.SetParent(sidebar, false);
            }
            if (combatPage is RectTransform combatRect)
            {
                combatRect.anchorMin = Vector2.zero;
                combatRect.anchorMax = Vector2.one;
                combatRect.offsetMin = new Vector2(14f, 14f);
                combatRect.offsetMax = new Vector2(-14f, -72f);
                combatRect.SetAsLastSibling();
            }

            CombatHudPresenter presenter = combatPage?
                .GetComponent<CombatHudPresenter>();
            WorldMapLocationView locationView = FindDescendant(
                    root,
                    "LocationView")?
                .GetComponent<WorldMapLocationView>();
            if (presenter == null || locationView == null)
                return;

            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("locationView").objectReferenceValue =
                locationView;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void ApplyToDialoguePrefabContents(GameObject root)
        {
            if (root == null)
                throw new MissingReferenceException(
                    "Light skin requires a DialoguePanel prefab.");

            Sprite panelSprite = LoadRequiredSprite(PanelSpritePath);
            StyleOptionalImage(root.transform, "DialoguePanel", panelSprite,
                ContentNavy);
            StyleOptionalImage(root.transform, "SpeakerHeader", panelSprite,
                HeaderNavy);
            StyleOptionalButton(root.transform, "ReplyButton", panelSprite);
            StyleOptionalButton(root.transform, "GoodbyeButton", panelSprite);
            EnsureReadableText(root.transform);
            EnsureBoldText(root.transform);
        }

        private static void StyleTopStatus(
            Transform root,
            Sprite darkButton)
        {
            Transform day = RequireDescendant(root, "DayTimeUI");
            ConfigureTopStatusRect(
                (RectTransform)day,
                24f,
                300f);
            StyleImage(
                day.GetComponent<Image>(),
                darkButton,
                HeaderNavy);
            EnsureShadow(
                day.gameObject,
                new Color(0f, 0f, 0.01f, 0.72f),
                new Vector2(0f, -5f));
            StyleText(day, "DayText", WarmIvory);
            StyleGraphic(day, "PaceImage", Green);
            StyleGraphic(day, "TimeProgress", Gold);

            Transform stats = RequireDescendant(root, "CardStatsUI");
            ConfigureTopStatusRect(
                (RectTransform)stats,
                324f,
                390f);
            StyleImage(
                stats.GetComponent<Image>(),
                darkButton,
                HeaderNavy);
            HorizontalLayoutGroup statsLayout =
                stats.GetComponent<HorizontalLayoutGroup>();
            if (statsLayout != null)
            {
                statsLayout.padding = new RectOffset(14, 18, 5, 5);
                statsLayout.spacing = 6f;
                statsLayout.childAlignment = TextAnchor.MiddleLeft;
            }
            EnsureShadow(
                stats.gameObject,
                new Color(0f, 0f, 0.01f, 0.72f),
                new Vector2(0f, -5f));
            StyleText(stats, "NutritionLabel", Green);
            StyleText(stats, "CurrencyLabel", Gold);
            StyleText(stats, "CardLabel", Cyan);
            StyleGraphic(stats, "NutritionIcon", Green);
            StyleGraphic(stats, "CurrencyIcon", Gold);
            StyleGraphic(stats, "CardIcon", Cyan);
        }

        private static void ConfigureTopStatusRect(
            RectTransform rect,
            float x,
            float width)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -18f);
            rect.sizeDelta = new Vector2(width, 64f);
        }

        private static void StyleCommonSidebar(
            Transform root,
            Sprite panelSprite,
            Sprite headerSprite)
        {
            Transform sidebar = RequireDescendant(root, "MenuPanel");
            RectTransform sidebarRect = (RectTransform)sidebar;
            sidebarRect.anchorMin = new Vector2(1f, 0f);
            sidebarRect.anchorMax = new Vector2(1f, 1f);
            sidebarRect.pivot = new Vector2(1f, 0.5f);
            sidebarRect.anchoredPosition = Vector2.zero;
            sidebarRect.sizeDelta = new Vector2(380f, 0f);

            Image sidebarImage = sidebar.GetComponent<Image>();
            if (sidebarImage == null)
                sidebarImage = sidebar.gameObject.AddComponent<Image>();
            StyleImage(sidebarImage, panelSprite, PanelNavy);
            sidebarImage.raycastTarget = true;
            EnsureShadow(
                sidebar.gameObject,
                new Color(0f, 0f, 0.01f, 0.74f),
                new Vector2(-7f, -6f));

            Transform header = RequireDescendant(sidebar, "Header");
            RectTransform headerRect = (RectTransform)header;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 64f);
            StyleImage(
                header.GetComponent<Image>(),
                headerSprite,
                HeaderNavy);

            HorizontalLayoutGroup headerLayout =
                header.GetComponent<HorizontalLayoutGroup>();
            if (headerLayout != null)
            {
                headerLayout.padding = new RectOffset(8, 8, 6, 6);
                headerLayout.spacing = 6f;
            }

            foreach (string pageName in new[]
                     {
                         "LocationView",
                         "QuestsView",
                         "RecipesView",
                         "BackpackTablePanel"
                     })
            {
                Transform page = FindDescendant(root, pageName);
                if (page == null)
                    continue;
                RectTransform pageRect = page as RectTransform;
                if (pageRect == null)
                    continue;
                pageRect.anchorMin = Vector2.zero;
                pageRect.anchorMax = Vector2.one;
                pageRect.offsetMin = new Vector2(14f, 14f);
                pageRect.offsetMax = new Vector2(-14f, -72f);
            }
        }

        private static void StyleLocationSidebar(
            Transform root,
            Sprite panelSprite,
            Sprite borderSprite,
            Sprite blueButton,
            Sprite purpleButton,
            Sprite brownButton,
            Sprite greenButton,
            Sprite selectionLight)
        {
            Transform sidebar = RequireDescendant(root, "MenuPanel");
            Transform header = RequireDescendant(sidebar, "Header");
            StyleImage(
                header.GetComponent<Image>(),
                blueButton,
                HeaderNavy);
            EnsureShadow(
                header.gameObject,
                new Color(0f, 0f, 0.01f, 0.62f),
                new Vector2(0f, -4f));

            StyleToggle(
                root,
                "LocationToggle",
                blueButton,
                selectionLight);
            StyleToggle(
                root,
                "QuestsToggle",
                purpleButton,
                selectionLight);
            StyleToggle(
                root,
                "RecipesToggle",
                brownButton,
                selectionLight);
            StyleToggle(
                root,
                "BackpackToggle",
                purpleButton,
                selectionLight);

            Transform view = RequireDescendant(root, "LocationView");
            StyleImage(
                view.GetComponent<Image>(),
                panelSprite,
                PanelNavy);
            EnsureShadow(
                view.gameObject,
                new Color(0f, 0f, 0.01f, 0.78f),
                new Vector2(-7f, -8f));
            Image border = EnsureOverlay(
                view,
                "LocationFantasyBorder",
                borderSprite,
                new Color(0.30f, 0.70f, 0.94f, 0.88f),
                new Vector2(0.008f, 0.006f),
                new Vector2(0.992f, 0.994f));
            border.transform.SetAsFirstSibling();

            Transform art = RequireDescendant(view, "LocationArt");
            Image artBorder = EnsureOverlay(
                art,
                "LocationArtFantasyBorder",
                borderSprite,
                new Color(0.76f, 0.66f, 0.44f, 0.86f),
                new Vector2(-0.025f, -0.025f),
                new Vector2(1.025f, 1.025f));
            artBorder.transform.SetAsLastSibling();

            StyleText(view, "LocationTitle", Cyan);
            StyleText(view, "LocationTypeAndDanger", Gold);
            StyleText(view, "LocationDiscovery", Silver);
            StyleText(view, "LocationTravelTime", Cyan);
            StyleText(view, "LocationResources", Green);
            StyleText(view, "LocationDescription", WarmIvory);
            StyleButton(
                root,
                "EnterLocationButton",
                greenButton,
                Green);
        }

        private static void StyleBackpack(
            Transform root,
            Sprite panelSprite,
            Sprite borderSprite,
            Sprite purpleButton,
            Sprite redButton,
            Sprite yellowButton)
        {
            Transform drawer = FindDescendant(root, "BackpackTablePanel");
            if (drawer != null)
            {
                Image drawerImage = drawer.GetComponent<Image>();
                if (drawerImage != null)
                    StyleImage(drawerImage, panelSprite, ContentNavy);
            }

            if (FindDescendant(root, "BackpackSidebarPageV2") != null ||
                FindDescendant(root, "BackpackDrawerLayoutV1") != null)
                return;

            StyleButton(
                root,
                "BackpackButton",
                purpleButton,
                Color.white);
            StyleButton(
                root,
                "BackpackCloseButton",
                redButton,
                Color.white);
            StyleButton(
                root,
                "BackpackArrangeButton",
                yellowButton,
                new Color(0.13f, 0.10f, 0.055f, 1f));

            Transform background =
                RequireDescendant(root, "BackpackBackground");
            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = Color.white;
            backgroundImage.preserveAspect = true;
            EnsureShadow(
                background.gameObject,
                new Color(0f, 0f, 0.01f, 0.72f),
                new Vector2(0f, -7f));
            Image border = EnsureOverlay(
                background,
                "BackpackFantasyBorder",
                borderSprite,
                new Color(0.72f, 0.54f, 0.30f, 0.92f),
                new Vector2(-0.012f, -0.018f),
                new Vector2(1.012f, 1.018f));
            border.transform.SetAsLastSibling();

            StyleText(
                root,
                "BackpackCapacityText",
                Gold);
        }

        private static void StyleInfoPanels(
            Transform root,
            Sprite panelSprite,
            Sprite borderSprite,
            Sprite listSprite,
            Sprite greenButton,
            Sprite brownButton,
            Sprite blueButton)
        {
            Transform infoPanel =
                RequireDescendant(root, "InfoPanel");
            StyleImage(
                infoPanel.GetComponent<Image>(),
                panelSprite,
                ContentNavy);
            EnsureShadow(
                infoPanel.gameObject,
                new Color(0f, 0f, 0.01f, 0.76f),
                new Vector2(0f, -7f));
            Image infoBorder = EnsureOverlay(
                infoPanel,
                "InfoPanelFantasyBorder",
                borderSprite,
                new Color(0.76f, 0.58f, 0.32f, 0.90f),
                new Vector2(-0.012f, -0.020f),
                new Vector2(1.012f, 1.020f));
            infoBorder.transform.SetAsFirstSibling();
            StyleText(infoPanel, "InfoText", WarmIvory);
            StyleText(infoPanel, "ActionButton", Gold);
            Transform actionButton =
                RequireDescendant(infoPanel, "ActionButton");
            LayoutElement actionLayout =
                actionButton.GetComponent<LayoutElement>();
            if (actionLayout == null)
                actionLayout = actionButton.gameObject.AddComponent<LayoutElement>();
            actionLayout.minWidth = 140f;
            actionLayout.preferredWidth = 140f;
            actionLayout.flexibleWidth = 0f;

            Transform npcPanel =
                RequireDescendant(root, "NpcTradePanel");
            StyleImage(
                npcPanel.GetComponent<Image>(),
                panelSprite,
                ContentNavy);
            EnsureShadow(
                npcPanel.gameObject,
                new Color(0f, 0f, 0.01f, 0.78f),
                new Vector2(0f, -6f));
            Image npcBorder = EnsureOverlay(
                npcPanel,
                "NpcTradeFantasyBorder",
                borderSprite,
                new Color(0.30f, 0.70f, 0.94f, 0.88f),
                new Vector2(-0.012f, -0.016f),
                new Vector2(1.012f, 1.016f));
            npcBorder.transform.SetAsFirstSibling();

            Transform scroll =
                RequireDescendant(npcPanel, "NpcTradeScrollView");
            StyleImage(
                scroll.GetComponent<Image>(),
                listSprite,
                ListNavy);
            EnsureShadow(
                scroll.gameObject,
                new Color(0f, 0f, 0.01f, 0.52f),
                new Vector2(0f, -3f));
            StyleText(npcPanel, "NpcTradeHint", Silver);
            StyleButton(
                npcPanel,
                "NpcBuyTabButton",
                greenButton,
                Green);
            StyleButton(
                npcPanel,
                "NpcSellTabButton",
                brownButton,
                Gold);
            StyleButton(
                npcPanel,
                "NpcActionTabButton",
                blueButton,
                WarmIvory);
        }

        private static void StylePartyAndJournalPanels(
            Transform root,
            Sprite panelSprite)
        {
            Transform partyPanel =
                RequireDescendant(root, "WorldMapPartyStatusPanel");
            StyleImage(
                partyPanel.GetComponent<Image>(),
                panelSprite,
                ContentNavy);
            EnsureShadow(
                partyPanel.gameObject,
                new Color(0f, 0f, 0.01f, 0.78f),
                new Vector2(0f, -7f));
            StyleText(partyPanel, "PanelTitle", Cyan);
            StyleText(partyPanel, "PartyName", Cyan);
            StyleText(partyPanel, "PartyHealthText", WarmIvory);
            StyleText(partyPanel, "PartyLocationText", Silver);
            StyleText(partyPanel, "PartyMembersText", WarmIvory);
            StyleText(partyPanel, "PartyStateText", Gold);
            StyleGraphic(partyPanel, "PartyHealthFill", Green);
            StyleGraphic(partyPanel, "PartyDivider", Silver);

            StyleJournalPanel(
                root,
                "QuestsView",
                panelSprite,
                QuestNavy);
            StyleJournalPanel(
                root,
                "RecipesView",
                panelSprite,
                RecipeBrown);
        }

        private static void StyleJournalPanel(
            Transform root,
            string panelName,
            Sprite panelSprite,
            Color color)
        {
            Transform panel = RequireDescendant(root, panelName);
            StyleImage(
                panel.GetComponent<Image>(),
                panelSprite,
                color);
            EnsureShadow(
                panel.gameObject,
                new Color(0f, 0f, 0.01f, 0.76f),
                new Vector2(-5f, -7f));

            MenuView menu = panel.GetComponent<MenuView>();
            if (menu != null)
            {
                var serialized = new SerializedObject(menu);
                serialized.FindProperty("headerColor").colorValue =
                    JournalHeaderInk;
                serialized.FindProperty("itemColor").colorValue = WarmIvory;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void StyleExtendedLightInterfaces(
            Transform root,
            Sprite panelSprite,
            Sprite borderSprite,
            Sprite listSprite)
        {
            foreach (string name in new[]
                     {
                         "BackpackSelectedDetails",
                         "CombatHudPanel",
                         "CombatLogPanel",
                         "PublicMarketModal",
                         "PublicMarketBackpackPanel",
                         "PublicMarketMarketPanel",
                         "PublicMarketTransactionPanel",
                         "PauseMenu"
                     })
            {
                StyleOptionalImage(
                    root,
                    name,
                    panelSprite,
                    ContentNavy);
                Transform surface = FindDescendant(root, name);
                if (surface != null)
                {
                    EnsureShadow(
                        surface.gameObject,
                        new Color(0.08f, 0.12f, 0.16f, 0.20f),
                        new Vector2(-2f, -3f));
                }
            }

            StyleOptionalImage(
                root,
                "PublicMarketFantasyHeader",
                panelSprite,
                HeaderNavy);
            StyleOptionalImage(
                root,
                "PublicMarketFantasyFrame",
                borderSprite,
                new Color(0.18f, 0.42f, 0.58f, 0.92f));
            StyleOptionalImage(
                root,
                "PublicMarketFantasyHeaderHighlight",
                borderSprite,
                new Color(0.30f, 0.70f, 0.94f, 0.64f));

            StyleOptionalImage(
                root,
                "PublicMarketBackpackPanelFantasyBorder",
                borderSprite,
                new Color(0.20f, 0.46f, 0.66f, 0.86f));
            StyleOptionalImage(
                root,
                "PublicMarketTransactionPanelFantasyBorder",
                borderSprite,
                new Color(0.62f, 0.39f, 0.10f, 0.86f));
            StyleOptionalImage(
                root,
                "PublicMarketMarketPanelFantasyBorder",
                borderSprite,
                new Color(0.18f, 0.52f, 0.34f, 0.86f));

            foreach (string name in new[]
                     {
                         "NpcTradeRowTemplate",
                         "PublicMarketBackpackPanelScrollView",
                         "PublicMarketMarketPanelScrollView",
                         "BackpackSlot1",
                         "BackpackSlot2",
                         "BackpackSlot3",
                         "BackpackSlot4",
                         "BackpackSlot5",
                         "BackpackSlot6",
                         "BackpackSlot7",
                         "BackpackSlot8"
                     })
            {
                StyleOptionalImage(root, name, listSprite, ListNavy);
            }

            StyleMarketRow(
                root,
                "PublicMarketBackpackRowTemplate",
                listSprite);
            StyleMarketRow(
                root,
                "PublicMarketMarketRowTemplate",
                listSprite);

            foreach (string name in new[]
                     {
                         "PublicMarketCloseButton",
                         "PublicMarketDecreaseButton",
                         "PublicMarketIncreaseButton",
                         "PublicMarketMaximumButton",
                         "PublicMarketConfirmButton",
                         "SkillButton1",
                         "SkillButton2",
                         "SkillButton3",
                         "RetreatButton"
                     })
            {
                StyleOptionalButton(root, name, panelSprite);
                if (name.StartsWith("SkillButton") ||
                    name == "RetreatButton")
                {
                    FitCombatActionButton(root, name);
                }
            }
            StyleOptionalButton(
                root,
                "PublicMarketCloseButton",
                panelSprite,
                new Color(0.78f, 0.60f, 0.58f, 1f),
                new Color(0.38f, 0.10f, 0.09f, 1f));
            StyleOptionalButton(
                root,
                "PublicMarketDecreaseButton",
                panelSprite,
                new Color(0.58f, 0.69f, 0.80f, 1f),
                WarmIvory);
            StyleOptionalButton(
                root,
                "PublicMarketIncreaseButton",
                panelSprite,
                new Color(0.58f, 0.69f, 0.80f, 1f),
                WarmIvory);
            StyleOptionalButton(
                root,
                "PublicMarketMaximumButton",
                panelSprite,
                new Color(0.78f, 0.64f, 0.40f, 1f),
                new Color(0.42f, 0.25f, 0.05f, 1f));
            StyleOptionalButton(
                root,
                "PublicMarketConfirmButton",
                panelSprite,
                new Color(0.50f, 0.72f, 0.55f, 1f),
                new Color(0.08f, 0.32f, 0.16f, 1f));

            foreach (string name in new[]
                     {
                         "BackpackTablePanel",
                         "BackpackSelectedDetails",
                         "InfoPanel",
                         "NpcTradePanel",
                         "NpcTradeScrollView",
                         "WorldMapPartyStatusPanel",
                         "QuestsView",
                         "RecipesView",
                         "CombatHudPanel",
                         "CombatLogPanel",
                         "PublicMarketModal"
                     })
            {
                Transform panel = FindDescendant(root, name);
                if (panel != null)
                    EnsureReadableText(panel);
            }

            StylePublicMarketLight(root, panelSprite, listSprite);
        }

        private static void StylePublicMarketLight(
            Transform root,
            Sprite panelSprite,
            Sprite listSprite)
        {
            StyleOptionalImage(
                root,
                "PublicMarketModal",
                panelSprite,
                MarketModalLight);
            StyleOptionalImage(
                root,
                "PublicMarketFantasyHeader",
                panelSprite,
                new Color(0.62f, 0.69f, 0.74f, 1f));
            StyleOptionalImage(
                root,
                "PublicMarketBackpackPanel",
                panelSprite,
                MarketBackpackLight);
            StyleOptionalImage(
                root,
                "PublicMarketTransactionPanel",
                panelSprite,
                MarketTransactionLight);
            StyleOptionalImage(
                root,
                "PublicMarketMarketPanel",
                panelSprite,
                MarketInventoryLight);
            StyleOptionalImage(
                root,
                "PublicMarketBackpackPanelScrollView",
                listSprite,
                new Color(0.69f, 0.78f, 0.84f, 1f));
            StyleOptionalImage(
                root,
                "PublicMarketMarketPanelScrollView",
                listSprite,
                new Color(0.69f, 0.81f, 0.73f, 1f));
            StyleOptionalImage(
                root,
                "PublicMarketSelectionCard",
                listSprite,
                new Color(0.82f, 0.72f, 0.53f, 1f));

            StyleOptionalText(
                root,
                "PublicMarketTitle",
                new Color(0.08f, 0.30f, 0.24f, 1f));
            StyleOptionalText(
                root,
                "PublicMarketSummary",
                new Color(0.27f, 0.31f, 0.35f, 1f));
            StyleOptionalText(
                root,
                "PublicMarketBackpackPanelTitle",
                new Color(0.07f, 0.29f, 0.45f, 1f));
            StyleOptionalText(
                root,
                "PublicMarketTransactionTitle",
                new Color(0.38f, 0.22f, 0.05f, 1f));
            StyleOptionalText(
                root,
                "PublicMarketMarketPanelTitle",
                new Color(0.07f, 0.31f, 0.17f, 1f));
            foreach (string name in new[]
                     {
                         "PublicMarketBackpackPanelSubtitle",
                         "PublicMarketMarketPanelSubtitle",
                         "PublicMarketTransactionGuide",
                         "PublicMarketBackpackEmptyState",
                         "PublicMarketMarketEmptyState",
                         "PublicMarketHint"
                     })
            {
                StyleOptionalText(
                    root,
                    name,
                    new Color(0.28f, 0.31f, 0.34f, 1f));
            }
            StyleOptionalText(
                root,
                "PublicMarketSelectionLabel",
                WarmIvory);
            StyleOptionalText(
                root,
                "PublicMarketQuantityLabel",
                Gold);
        }

        private static void StyleMarketRow(
            Transform root,
            string name,
            Sprite sprite)
        {
            Transform row = FindDescendant(root, name);
            if (row == null)
                return;
            StyleOptionalImage(row, name, sprite, ListNavy);
            EnsureShadow(
                row.gameObject,
                new Color(0.08f, 0.12f, 0.16f, 0.18f),
                new Vector2(0f, -2f));

            MarketCommodityListItem item =
                row.GetComponent<MarketCommodityListItem>();
            if (item == null)
                return;
            var serialized = new SerializedObject(item);
            serialized.FindProperty("buyButtonSprite")
                .objectReferenceValue = sprite;
            serialized.FindProperty("sellButtonSprite")
                .objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void StyleOptionalButton(
            Transform root,
            string name,
            Sprite sprite)
        {
            StyleOptionalButton(
                root,
                name,
                sprite,
                LightButtonBlue,
                WarmIvory);
        }

        private static void StyleOptionalButton(
            Transform root,
            string name,
            Sprite sprite,
            Color background,
            Color text)
        {
            Transform target = FindDescendant(root, name);
            if (target == null || target.GetComponent<Button>() == null)
                return;
            StyleButton(
                target,
                sprite,
                background,
                text);
        }

        private static void FitCombatActionButton(
            Transform root,
            string name)
        {
            RectTransform rect =
                FindDescendant(root, name) as RectTransform;
            if (rect == null)
                return;
            float y = rect.anchoredPosition.y;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(-44f, rect.sizeDelta.y);
        }

        private static void StyleOptionalImage(
            Transform root,
            string name,
            Sprite sprite,
            Color color)
        {
            Transform target = FindDescendant(root, name);
            Image image = target?.GetComponent<Image>();
            if (image != null)
                StyleImage(image, sprite, color);
        }

        private static void StyleOptionalText(
            Transform root,
            string name,
            Color color)
        {
            TMP_Text label = FindDescendant(root, name)
                ?.GetComponent<TMP_Text>();
            if (label != null)
                label.color = color;
        }

        private static void EnsureReadableText(Transform root)
        {
            TMP_Text[] labels =
                root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text label in labels)
            {
                float luminance = ColorLuminance(label.color);
                if (luminance <= 0.52f)
                    continue;

                const float targetLuminance = 0.28f;
                float darken = Mathf.Clamp01(
                    (luminance - targetLuminance) /
                    Mathf.Max(0.001f, luminance));
                Color darkened = Color.Lerp(label.color, Color.black, darken);
                darkened.a = label.color.a;
                label.color = darkened;
            }
        }

        private static void EnsureBoldText(Transform root)
        {
            foreach (TMP_Text label in
                     root.GetComponentsInChildren<TMP_Text>(true))
            {
                label.fontStyle |= FontStyles.Bold;
            }
        }

        private static float ColorLuminance(Color color)
        {
            return 0.2126f * color.r +
                   0.7152f * color.g +
                   0.0722f * color.b;
        }

        internal static void StyleLocationReturnButton(Button button)
        {
            if (button == null)
                throw new MissingReferenceException(
                    "Location return button is missing.");
            StyleButton(
                button.transform,
                LoadRequiredSprite(PanelSpritePath),
                LightButtonBlue,
                WarmIvory);
            EnsureBoldText(button.transform);
        }

        private static void StyleToggle(
            Transform root,
            string name,
            Sprite sprite,
            Sprite selectionLight)
        {
            Transform target = RequireDescendant(root, name);
            Image image = target.GetComponent<Image>();
            StyleImage(image, sprite, LightButtonBlue);
            EnsureShadow(
                target.gameObject,
                new Color(0f, 0f, 0.01f, 0.65f),
                new Vector2(0f, -4f));

            Toggle toggle = target.GetComponent<Toggle>();
            toggle.targetGraphic = image;
            Image selection = EnsureOverlay(
                target,
                "FantasySelectionHighlight",
                selectionLight,
                new Color(1f, 0.91f, 0.52f, 1f),
                Vector2.zero,
                Vector2.one);
            selection.transform.SetAsLastSibling();
            toggle.graphic = selection;
            toggle.transition = Selectable.Transition.ColorTint;
            toggle.colors = CreateSelectableColors();
            toggle.navigation = new Navigation
            {
                mode = Navigation.Mode.Automatic
            };
            TMP_Text label = target
                .GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.color = WarmIvory;
        }

        private static void StyleButton(
            Transform root,
            string name,
            Sprite sprite,
            Color labelColor)
        {
            Transform target = RequireDescendant(root, name);
            StyleButton(
                target,
                sprite,
                LightButtonBlue,
                labelColor);
        }

        private static void StyleButton(
            Transform target,
            Sprite sprite,
            Color fillColor,
            Color labelColor)
        {
            Image image = target.GetComponent<Image>();
            StyleImage(image, sprite, fillColor);
            EnsureShadow(
                target.gameObject,
                new Color(0f, 0f, 0.01f, 0.68f),
                new Vector2(0f, -5f));

            Button button = target.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CreateSelectableColors();
            button.navigation = new Navigation
            {
                mode = Navigation.Mode.Automatic
            };
            TMP_Text label = target
                .GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.color = labelColor;
        }

        private static ColorBlock CreateSelectableColors()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor =
                new Color(0.96f, 0.96f, 0.96f, 1f);
            colors.pressedColor =
                new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor =
                new Color(0.92f, 0.92f, 1f, 1f);
            colors.disabledColor =
                new Color(0.50f, 0.50f, 0.50f, 0.68f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        private static void StyleText(
            Transform root,
            string name,
            Color color)
        {
            TMP_Text label = RequireDescendant(root, name)
                .GetComponent<TMP_Text>();
            label.color = color;
        }

        private static void StyleGraphic(
            Transform root,
            string name,
            Color color)
        {
            Graphic graphic = RequireDescendant(root, name)
                .GetComponent<Graphic>();
            if (graphic != null)
                graphic.color = color;
        }

        private static void StyleImage(
            Image image,
            Sprite sprite,
            Color color)
        {
            if (image == null)
                throw new MissingComponentException(
                    "Common fantasy HUD element requires an Image.");
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
        }

        private static Shadow EnsureShadow(
            GameObject target,
            Color color,
            Vector2 distance)
        {
            Shadow shadow = null;
            Shadow[] shadows = target.GetComponents<Shadow>();
            for (int i = 0; i < shadows.Length; i++)
            {
                if (shadows[i].GetType() == typeof(Shadow))
                {
                    shadow = shadows[i];
                    break;
                }
            }

            if (shadow == null)
                shadow = target.AddComponent<Shadow>();
            color.a = Mathf.Min(color.a, 0.22f);
            shadow.effectColor = color;
            shadow.effectDistance = Vector2.ClampMagnitude(distance, 4f);
            shadow.useGraphicAlpha = true;
            return shadow;
        }

        private static Image EnsureOverlay(
            Transform parent,
            string name,
            Sprite sprite,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            Transform existing = FindDirectChild(parent, name);
            GameObject overlay;
            if (existing == null)
            {
                overlay = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                overlay.layer = LayerMask.NameToLayer("UI");
                overlay.transform.SetParent(parent, false);
            }
            else
            {
                overlay = existing.gameObject;
            }

            RectTransform rect =
                (RectTransform)overlay.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            Image image = overlay.GetComponent<Image>();
            StyleImage(image, sprite, color);
            image.raycastTarget = false;

            LayoutElement layoutElement =
                overlay.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = overlay.AddComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            return image;
        }

        private static Sprite LoadRequiredSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                throw new MissingReferenceException(
                    $"Fantasy RPG UI sprite is missing: {path}");
            return sprite;
        }

        private static Transform RequireDescendant(
            Transform root,
            string name)
        {
            Transform result = FindDescendant(root, name);
            if (result == null)
                throw new MissingReferenceException(
                    $"Common HUD element is missing: {name}");
            return result;
        }

        private static Transform FindDescendant(
            Transform root,
            string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendant(
                    root.GetChild(i),
                    name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static Transform FindDescendant(
            Scene scene,
            string name)
        {
            if (!scene.IsValid())
                return null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindDescendant(
                    roots[i].transform,
                    name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static Transform FindDirectChild(
            Transform root,
            string name)
        {
            if (root == null)
                return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name)
                    return child;
            }
            return null;
        }
    }
}
