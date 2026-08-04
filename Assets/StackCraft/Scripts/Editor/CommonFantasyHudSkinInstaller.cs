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
        private const string LocationScenePath =
            "Assets/StackCraft/Scenes/Location.unity";
        private const string ComponentRoot =
            "Assets/Layer Lab/GUI Pro-FantasyRPG/ResourcesData/" +
            "Sprites/Component/";
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
        private const string ButtonRoot =
            ComponentRoot + "Button/";
        private const string LocationReturnButtonSpritePath =
            ButtonRoot + "Button_Rectangle_01_Convex_Blue.Png";
        private const string DarkButtonSpritePath = PanelSpritePath;
        private const string BlueButtonSpritePath = PanelSpritePath;
        private const string PurpleButtonSpritePath = BlueButtonSpritePath;
        private const string BrownButtonSpritePath = BlueButtonSpritePath;
        private const string GreenButtonSpritePath = BlueButtonSpritePath;
        private const string RedButtonSpritePath = BlueButtonSpritePath;
        private const string YellowButtonSpritePath = BlueButtonSpritePath;
        private const string SelectionLightSpritePath = BorderSpritePath;

        private static readonly Color PanelNavy =
            new(0.075f, 0.070f, 0.085f, 0.985f);
        private static readonly Color HeaderNavy =
            new(0.145f, 0.125f, 0.135f, 1f);
        private static readonly Color ModernDarkButtonNavy =
            new(0.169f, 0.235f, 0.341f, 1f);
        private static readonly Color ContentNavy =
            new(0.095f, 0.090f, 0.105f, 1f);
        private static readonly Color ListNavy =
            new(0.115f, 0.108f, 0.120f, 1f);
        private static readonly Color QuestNavy =
            new(0.095f, 0.090f, 0.105f, 1f);
        private static readonly Color RecipeBrown =
            new(0.105f, 0.095f, 0.085f, 1f);
        private static readonly Color Cyan =
            new(0.38f, 0.82f, 0.97f, 1f);
        private static readonly Color WarmIvory =
            new(0.96f, 0.87f, 0.71f, 1f);
        private static readonly Color Gold =
            new(0.98f, 0.75f, 0.30f, 1f);
        private static readonly Color Green =
            new(0.58f, 0.88f, 0.62f, 1f);
        private static readonly Color Silver =
            new(0.72f, 0.78f, 0.82f, 1f);

        [MenuItem("Tools/StackCraft/Apply Common Fantasy HUD Skin")]
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
                AssetDatabase.SaveAssets();
                Debug.Log("Applied the common fantasy HUD skin.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
        }

        internal static void StyleLocationReturnButton(Button button)
        {
            if (button == null)
                throw new MissingReferenceException(
                    "Location return button is missing.");
            StyleButton(
                button.transform,
                LoadRequiredSprite(LocationReturnButtonSpritePath),
                Color.white,
                Color.white);
        }

        private static void StyleToggle(
            Transform root,
            string name,
            Sprite sprite,
            Sprite selectionLight)
        {
            Transform target = RequireDescendant(root, name);
            Image image = target.GetComponent<Image>();
            StyleImage(image, sprite, ModernDarkButtonNavy);
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
                label.color = Color.white;
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
                ModernDarkButtonNavy,
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
            shadow.effectColor = color;
            shadow.effectDistance = distance;
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
