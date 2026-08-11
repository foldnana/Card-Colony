using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CardColony.Tests
{
    public sealed class CommonFantasyHudUnityTests
    {
        private const string UiRootPath =
            "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";
        private const string DialoguePanelPath =
            "Assets/StackCraft/Prefabs/UI/DialoguePanel.prefab";
        private const string ComponentRoot =
            "Assets/Layer Lab/GUI Pro-FantasyRPG/ResourcesData/" +
            "Sprites/Component/";
        private const string ButtonRoot =
            ComponentRoot + "Button/";
        private const string UltimateShapeRoot =
            "Assets/UltimateCleanGUIPack/Common/Sprites/Shapes/";
        private const string UltimateRoundedFill =
            UltimateShapeRoot +
            "Semi Rounded/Semi Rounded - 300ppu.png";
        private const string UltimateRoundedOutline =
            UltimateShapeRoot +
            "Semi Rounded/Semi Rounded - Outline - 6px - 300ppu.png";
        private const string UltimateModernDarkButtonFill =
            UltimateShapeRoot +
            "Semi Rounded/Semi Rounded - 300ppu.png";
        private const string UltimateModernDarkButtonOutline =
            UltimateShapeRoot +
            "Semi Rounded/Semi Rounded - Outline - 6px - 300ppu.png";

        [Test]
        public void UiRoot_CommonFantasyHudStylesLocationSidebarAndTopStatus()
        {
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPath);
            Assert.That(root, Is.Not.Null);

            Transform day = FindDescendant(root.transform, "DayTimeUI");
            Transform stats = FindDescendant(root.transform, "CardStatsUI");
            AssertSlicedSprite(
                day.GetComponent<Image>(),
                UltimateRoundedFill);
            AssertSlicedSprite(
                stats.GetComponent<Image>(),
                UltimateRoundedFill);
            AssertDepthShadow(day);
            AssertDepthShadow(stats);

            TMP_Text dayText = FindDescendant(
                day,
                "DayText").GetComponent<TMP_Text>();
            TMP_Text nutrition = FindDescendant(
                stats,
                "NutritionLabel").GetComponent<TMP_Text>();
            TMP_Text currency = FindDescendant(
                stats,
                "CurrencyLabel").GetComponent<TMP_Text>();
            TMP_Text cards = FindDescendant(
                stats,
                "CardLabel").GetComponent<TMP_Text>();
            Assert.That(ColorLuminance(dayText.color),
                Is.LessThan(0.35f),
                "日期需要使用暖象牙白。");
            Assert.That(nutrition.color.g,
                Is.GreaterThan(nutrition.color.r + 0.08f),
                "食物统计需要使用绿色。");
            Assert.That(currency.color.r,
                Is.GreaterThan(currency.color.b + 0.25f),
                "金币统计需要使用金色。");
            Assert.That(cards.color.b,
                Is.GreaterThan(cards.color.r + 0.12f),
                "卡牌容量需要使用青蓝色。");

            Transform locationView =
                FindDescendant(root.transform, "LocationView");
            AssertSlicedSprite(
                locationView.GetComponent<Image>(),
                UltimateRoundedFill);
            AssertDepthShadow(locationView);
            Transform border = FindDescendant(
                locationView,
                "LocationFantasyBorder");
            Assert.That(border, Is.Not.Null);
            AssertSlicedSprite(
                border.GetComponent<Image>(),
                UltimateRoundedOutline);
            Assert.That(border.GetComponent<Image>().raycastTarget, Is.False);

            AssertToggle(
                root,
                "LocationToggle",
                UltimateModernDarkButtonFill);
            AssertToggle(
                root,
                "QuestsToggle",
                UltimateModernDarkButtonFill);
            AssertToggle(
                root,
                "RecipesToggle",
                UltimateModernDarkButtonFill);
            AssertButton(
                root,
                "EnterLocationButton",
                UltimateModernDarkButtonFill);

            TMP_Text title = FindDescendant(
                locationView,
                "LocationTitle").GetComponent<TMP_Text>();
            TMP_Text type = FindDescendant(
                locationView,
                "LocationTypeAndDanger").GetComponent<TMP_Text>();
            TMP_Text description = FindDescendant(
                locationView,
                "LocationDescription").GetComponent<TMP_Text>();
            Assert.That(title.color.b,
                Is.GreaterThan(title.color.r + 0.12f));
            Assert.That(type.color.r,
                Is.GreaterThan(type.color.b + 0.20f));
            Assert.That(ColorLuminance(description.color),
                Is.LessThan(0.35f));
        }

        [Test]
        public void UiRoot_CommonFantasyHudKeepsSerializedBackpackSidebarPage()
        {
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPath);
            Assert.That(root, Is.Not.Null);

            Transform backpackRoot =
                FindDescendant(root.transform, "BackpackRoot");
            Assert.That(
                FindDescendant(backpackRoot, "BackpackSidebarPageV2"),
                Is.Not.Null,
                "背包侧栏页面必须直接序列化在 UIRoot.prefab 中。");
            Transform drawer = FindDescendant(
                root.transform,
                "BackpackTablePanel");
            Image drawerImage = drawer.GetComponent<Image>();
            Assert.That(drawerImage.enabled, Is.True);
            Assert.That(
                drawerImage.sprite,
                Is.EqualTo(FindDescendant(root.transform, "LocationView")
                    .GetComponent<Image>().sprite));
            Assert.That(drawerImage.color.a, Is.GreaterThanOrEqualTo(0.95f));
            Assert.That(
                FindDescendant(drawer, "BackpackBackground"),
                Is.Null,
                "旧的中央皮革桌面不能残留在右侧抽屉中。");
            Assert.That(
                FindDescendant(drawer, "BackpackSelectedDetails"),
                Is.Not.Null);

            Assert.That(FindDescendant(root.transform, "BackpackButton"), Is.Null);
            Assert.That(FindDescendant(root.transform, "BackpackCloseButton"), Is.Null);
            Transform arrange = FindDescendant(drawer, "BackpackArrangeButton");
            Assert.That(arrange.GetComponent<Image>().sprite, Is.Null);
            Transform backpackTab = FindDescendant(root.transform, "BackpackToggle");
            Transform locationTab = FindDescendant(root.transform, "LocationToggle");
            Assert.That(
                backpackTab.GetComponent<Toggle>().group,
                Is.SameAs(locationTab.GetComponent<Toggle>().group));

            TMP_Text capacity = FindDescendant(
                root.transform,
                "BackpackCapacityText").GetComponent<TMP_Text>();
            Assert.That(capacity.color.b,
                Is.GreaterThanOrEqualTo(capacity.color.r),
                "背包容量文字应使用清晰的冷白色，而不是旧皮革板金色。");

            MonoBehaviour view = backpackRoot
                .GetComponents<MonoBehaviour>()
                .Single(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.BackpackView");
            var serialized = new SerializedObject(view);
            foreach (string property in new[]
                     {
                         "tabToggle",
                         "fallbackToggle",
                         "tablePanel",
                         "capacityLabel",
                          "arrangeButton",
                          "slotsRoot",
                          "dragLayer",
                          "selectedNameLabel",
                          "selectedTypeLabel",
                          "selectedDescriptionLabel"
                     })
            {
                Assert.That(
                    serialized.FindProperty(property)
                        ?.objectReferenceValue,
                    Is.Not.Null,
                    $"预制体必须保存 BackpackView.{property} 绑定。");
            }
            Assert.That(serialized.FindProperty("openButton").objectReferenceValue,
                Is.Null);
            Assert.That(serialized.FindProperty("closeButton").objectReferenceValue,
                Is.Null);
        }

        [Test]
        public void UiRoot_CommonFantasyHudStylesInfoAndNpcContentPanels()
        {
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPath);
            Assert.That(root, Is.Not.Null);

            Transform infoPanel =
                FindDescendant(root.transform, "InfoPanel");
            AssertSlicedSprite(
                infoPanel.GetComponent<Image>(),
                UltimateRoundedFill);
            Assert.That(infoPanel.GetComponent<Image>().color.a,
                Is.EqualTo(1f));
            AssertDepthShadow(infoPanel);
            Transform infoBorder = FindDescendant(
                infoPanel,
                "InfoPanelFantasyBorder");
            AssertSlicedSprite(
                infoBorder.GetComponent<Image>(),
                UltimateRoundedOutline);
            Assert.That(infoBorder.GetComponent<Image>().raycastTarget,
                Is.False);
            LayoutElement infoBorderLayout =
                infoBorder.GetComponent<LayoutElement>();
            Assert.That(infoBorderLayout, Is.Not.Null);
            Assert.That(infoBorderLayout.ignoreLayout, Is.True,
                "消息栏装饰边框不能参与 VerticalLayoutGroup 排版。");

            Transform npcPanel =
                FindDescendant(root.transform, "NpcTradePanel");
            AssertSlicedSprite(
                npcPanel.GetComponent<Image>(),
                UltimateRoundedFill);
            Assert.That(npcPanel.GetComponent<Image>().color.a,
                Is.EqualTo(1f),
                "NPC 内容底板不应继续使用半透明黑色。");
            AssertDepthShadow(npcPanel);
            Transform npcBorder = FindDescendant(
                npcPanel,
                "NpcTradeFantasyBorder");
            AssertSlicedSprite(
                npcBorder.GetComponent<Image>(),
                UltimateRoundedOutline);
            Assert.That(npcBorder.GetComponent<Image>().raycastTarget,
                Is.False);

            Transform scroll =
                FindDescendant(npcPanel, "NpcTradeScrollView");
            AssertSlicedSprite(
                scroll.GetComponent<Image>(),
                UltimateRoundedFill);
            Assert.That(scroll.GetComponent<Image>().color.a,
                Is.EqualTo(1f),
                "NPC 列表底板不应继续透出地图。");

            AssertButton(
                root,
                "NpcBuyTabButton",
                UltimateModernDarkButtonFill);
            AssertButton(
                root,
                "NpcSellTabButton",
                UltimateModernDarkButtonFill);
            AssertButton(
                root,
                "NpcActionTabButton",
                UltimateModernDarkButtonFill);

            MonoBehaviour infoView = infoPanel
                .GetComponents<MonoBehaviour>()
                .Single(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.InfoPanel");
            var serializedInfo = new SerializedObject(infoView);
            Assert.That(
                serializedInfo.FindProperty("infoText")
                    ?.objectReferenceValue,
                Is.EqualTo(
                    FindDescendant(infoPanel, "InfoText")
                        .GetComponent<TMP_Text>()));
            Assert.That(
                serializedInfo.FindProperty("actionButton")
                    ?.objectReferenceValue,
                Is.EqualTo(
                    FindDescendant(infoPanel, "ActionButton")
                        .GetComponents<MonoBehaviour>()
                        .Single(component =>
                            component.GetType().FullName ==
                            "CryingSnow.StackCraft.TextButton")));

            Transform locationView =
                FindDescendant(root.transform, "LocationView");
            MonoBehaviour location = locationView
                .GetComponents<MonoBehaviour>()
                .Single(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.WorldMapLocationView");
            var serializedLocation = new SerializedObject(location);
            Assert.That(
                serializedLocation.FindProperty("npcTradePanel")
                    ?.objectReferenceValue,
                Is.EqualTo(npcPanel.gameObject));
            Assert.That(
                serializedLocation.FindProperty("npcBuyTabButton")
                    ?.objectReferenceValue,
                Is.EqualTo(
                    FindDescendant(npcPanel, "NpcBuyTabButton")
                        .GetComponent<Button>()));
            Assert.That(
                serializedLocation.FindProperty("npcSellTabButton")
                    ?.objectReferenceValue,
                Is.EqualTo(
                    FindDescendant(npcPanel, "NpcSellTabButton")
                        .GetComponent<Button>()));
            Assert.That(
                serializedLocation.FindProperty("npcActionTabButton")
                    ?.objectReferenceValue,
                Is.EqualTo(
                    FindDescendant(npcPanel, "NpcActionTabButton")
                        .GetComponent<Button>()));
        }

        [Test]
        public void UiRoot_CommonFantasyHudStylesWorldMapPartyStatusPanel()
        {
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPath);
            Assert.That(root, Is.Not.Null);

            Transform panel = FindDescendant(
                root.transform,
                "WorldMapPartyStatusPanel");
            AssertSlicedSprite(
                panel.GetComponent<Image>(),
                UltimateRoundedFill);
            Assert.That(panel.GetComponent<Image>().color.a, Is.EqualTo(1f));
            AssertDepthShadow(panel);

            TMP_Text partyName = FindDescendant(
                panel,
                "PartyName").GetComponent<TMP_Text>();
            TMP_Text partyState = FindDescendant(
                panel,
                "PartyStateText").GetComponent<TMP_Text>();
            Image healthFill = FindDescendant(
                panel,
                "PartyHealthFill").GetComponent<Image>();
            Assert.That(
                partyName.color.b,
                Is.GreaterThan(partyName.color.r + 0.10f));
            Assert.That(
                partyState.color.r,
                Is.GreaterThan(partyState.color.b + 0.18f));
            Assert.That(
                healthFill.color.g,
                Is.GreaterThan(healthFill.color.r + 0.08f));
        }

        [Test]
        public void UiRoot_CommonFantasyHudStylesQuestAndRecipeContentPanels()
        {
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPath);
            Assert.That(root, Is.Not.Null);

            AssertJournalPanel(
                root,
                "QuestsView");
            AssertJournalPanel(
                root,
                "RecipesView");
        }

        [Test]
        public void UiRoot_CommonFantasyHudUsesBoldTextAndDarkJournalHeaders()
        {
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPath);
            Assert.That(root, Is.Not.Null);

            AssertAllTextIsBold(root.transform);

            foreach (string panelName in new[] { "QuestsView", "RecipesView" })
            {
                MonoBehaviour menu = FindDescendant(root.transform, panelName)
                    .GetComponents<MonoBehaviour>()
                    .Single(component => component.GetType().BaseType
                        ?.FullName == "CryingSnow.StackCraft.MenuView");
                Color headerColor = new SerializedObject(menu)
                    .FindProperty("headerColor").colorValue;
                Assert.That(
                    ColorLuminance(headerColor),
                    Is.LessThanOrEqualTo(0.32f),
                    $"{panelName} 的运行时分组标题必须使用高对比深色文字。");
                Color itemColor = new SerializedObject(menu)
                    .FindProperty("itemColor").colorValue;
                Assert.That(
                    ColorLuminance(itemColor),
                    Is.LessThanOrEqualTo(0.35f),
                    $"{panelName} 的运行时条目文字必须使用高对比深色文字。");
            }

            GameObject dialogue =
                AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePanelPath);
            Assert.That(dialogue, Is.Not.Null);
            AssertAllTextIsBold(dialogue.transform);
        }

        [Test]
        public void TextButton_PreservesBoldWeightAcrossPointerStates()
        {
            System.Type textButtonType = System.AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "CryingSnow.StackCraft.TextButton"))
                .First(type => type != null);
            var go = new GameObject(
                "TextButtonTest",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            try
            {
                Component button = go.AddComponent(textButtonType);
                TMP_Text label = go.GetComponent<TMP_Text>();

                textButtonType.GetMethod("Setup").Invoke(
                    button,
                    new object[] { "任务标题", 30f, null, null });
                Assert.That(label.fontStyle.HasFlag(FontStyles.Bold), Is.True);

                textButtonType.GetMethod("OnPointerEnter")
                    .Invoke(button, new object[] { null });
                Assert.That(label.fontStyle.HasFlag(FontStyles.Bold), Is.True);
                Assert.That(label.fontStyle.HasFlag(FontStyles.Underline), Is.True);

                textButtonType.GetMethod("OnPointerExit")
                    .Invoke(button, new object[] { null });
                Assert.That(label.fontStyle.HasFlag(FontStyles.Bold), Is.True);
                Assert.That(label.fontStyle.HasFlag(FontStyles.Underline), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UiRoot_CommonFantasyHudAvoidsWashedOutSharedSurfaces()
        {
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPath);
            Assert.That(root, Is.Not.Null);

            foreach (string name in new[]
                     {
                         "MenuPanel",
                         "LocationView",
                         "InfoPanel",
                         "NpcTradePanel",
                         "WorldMapPartyStatusPanel",
                         "BackpackTablePanel",
                         "QuestsView",
                         "RecipesView"
                     })
            {
                Image surface = FindDescendant(root.transform, name)
                    .GetComponent<Image>();
                Assert.That(
                    ColorLuminance(surface.color),
                    Is.InRange(0.56f, 0.76f),
                    $"{name} 应使用中等明度的 Light 表面，不能接近白色。");
            }

            foreach (string name in new[]
                     {
                         "DayTimeUI",
                         "CardStatsUI",
                         "LocationToggle",
                         "QuestsToggle",
                         "RecipesToggle",
                         "BackpackToggle"
                     })
            {
                Image surface = FindDescendant(root.transform, name)
                    .GetComponent<Image>();
                Assert.That(
                    ColorLuminance(surface.color),
                    Is.InRange(0.54f, 0.70f),
                    $"{name} 的顶部和页签底板不能继续泛白。");
            }

            Image npcList = FindDescendant(
                root.transform,
                "NpcTradeScrollView").GetComponent<Image>();
            Assert.That(
                ColorLuminance(npcList.color),
                Is.InRange(0.56f, 0.72f),
                "列表底板需要比内容面板再深一级。" );
        }

        [Test]
        public void LocationScene_ReturnButtonUsesFantasyStyleAndKeepsBinding()
        {
            const string locationScenePath =
                "Assets/StackCraft/Scenes/Location.unity";
            Scene scene = SceneManager.GetSceneByPath(locationScenePath);
            bool openedByTest = !scene.IsValid() || !scene.isLoaded;
            if (openedByTest)
            {
                scene = EditorSceneManager.OpenScene(
                    locationScenePath,
                    OpenSceneMode.Additive);
            }
            try
            {
                Transform returnTransform = scene
                    .GetRootGameObjects()
                    .Select(root => FindDescendant(
                        root.transform,
                        "ReturnToWorldMapButton"))
                    .FirstOrDefault(result => result != null);
                Assert.That(returnTransform, Is.Not.Null);
                AssertSlicedSprite(
                    returnTransform.GetComponent<Image>(),
                    UltimateRoundedFill);
                Assert.That(
                    ColorLuminance(
                        returnTransform.GetComponent<Image>().color),
                    Is.InRange(0.62f, 0.92f));
                Button returnButton =
                    returnTransform.GetComponent<Button>();
                Assert.That(
                    returnButton.targetGraphic,
                    Is.EqualTo(
                        returnTransform.GetComponent<Image>()));
                Assert.That(
                    returnButton.navigation.mode,
                    Is.EqualTo(Navigation.Mode.Automatic));
                AssertDepthShadow(returnTransform);
                AssertAllTextIsBold(returnTransform);

                MonoBehaviour controller = scene
                    .GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<MonoBehaviour>(true))
                    .Single(component => component.GetType().FullName ==
                        "CryingSnow.StackCraft.LocationSceneController");
                Assert.That(
                    new SerializedObject(controller)
                        .FindProperty("returnButton")
                        ?.objectReferenceValue,
                    Is.EqualTo(returnButton),
                    "换肤不能替换地点场景控制器的返回按钮绑定。");
            }
            finally
            {
                if (openedByTest &&
                    scene.IsValid() &&
                    scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void LocationSkinInstaller_DoesNotSaveOrCloseBorrowedScene()
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var returnObject = new GameObject(
                "ReturnToWorldMapButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            SceneManager.MoveGameObjectToScene(returnObject, scene);

            bool sceneWasSaved = false;
            bool sceneWasClosed = false;
            void OnSceneSaved(Scene savedScene)
            {
                if (savedScene.handle == scene.handle)
                    sceneWasSaved = true;
            }
            void OnSceneUnloaded(Scene unloadedScene)
            {
                if (unloadedScene.handle == scene.handle)
                    sceneWasClosed = true;
            }

            EditorSceneManager.sceneSaved += OnSceneSaved;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            try
            {
                EditorSceneManager.MarkSceneDirty(scene);
                System.Type installerType = System.AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(assembly => assembly.GetType(
                        "CryingSnow.StackCraft.EditorTools." +
                        "CommonFantasyHudSkinInstaller"))
                    .First(type => type != null);
                MethodInfo applyBorrowedScene = installerType.GetMethod(
                    "ApplyLocationScene",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(Scene), typeof(bool) },
                    null);
                Assert.That(applyBorrowedScene, Is.Not.Null,
                    "场景换肤需要显式区分自己打开的场景和用户已打开的场景。");

                Assert.That(
                    applyBorrowedScene.ReturnType,
                    Is.EqualTo(typeof(bool)),
                    "The scene styling operation must report whether " +
                    "the caller may safely close the scene.");

                object mayCloseScene = applyBorrowedScene.Invoke(
                    null,
                    new object[] { scene, false });

                Assert.That(mayCloseScene, Is.EqualTo(false));
                Assert.That(scene.IsValid(), Is.True);
                Assert.That(scene.isLoaded, Is.True,
                    "换肤不能关闭调用方拥有的地点场景。");
                Assert.That(scene.isDirty, Is.True,
                    "换肤不能偷偷保存调用方尚未保存的场景。");
                Assert.That(sceneWasSaved, Is.False);
                Assert.That(sceneWasClosed, Is.False);
            }
            finally
            {
                EditorSceneManager.sceneSaved -= OnSceneSaved;
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void AssertToggle(
            GameObject root,
            string name,
            string spriteFileName)
        {
            Transform target = FindDescendant(root.transform, name);
            AssertSlicedSprite(
                target.GetComponent<Image>(),
                ResolveExpectedPath(spriteFileName));
            Toggle toggle = target.GetComponent<Toggle>();
            Assert.That(
                toggle.navigation.mode,
                Is.EqualTo(Navigation.Mode.Automatic));
            Assert.That(
                toggle.targetGraphic,
                Is.EqualTo(target.GetComponent<Image>()));
            Assert.That(toggle.graphic, Is.Not.Null);
            Assert.That(
                toggle.graphic,
                Is.Not.SameAs(toggle.targetGraphic),
                "Toggle 的选中淡入层不能与常驻彩色按钮底图共用。");
            AssertSlicedSprite(
                toggle.graphic as Image,
                UltimateModernDarkButtonOutline);
            Assert.That(toggle.graphic.raycastTarget, Is.False);
            AssertDepthShadow(target);
        }

        private static void AssertButton(
            GameObject root,
            string name,
            string spriteFileName)
        {
            Transform target = FindDescendant(root.transform, name);
            AssertSlicedSprite(
                target.GetComponent<Image>(),
                ResolveExpectedPath(spriteFileName));
            Button button = target.GetComponent<Button>();
            Assert.That(
                button.targetGraphic,
                Is.EqualTo(target.GetComponent<Image>()));
            Assert.That(button.colors.normalColor, Is.EqualTo(Color.white));
            Assert.That(
                button.navigation.mode,
                Is.EqualTo(Navigation.Mode.Automatic));
            AssertDepthShadow(target);
        }

        private static void AssertJournalPanel(
            GameObject root,
            string panelName)
        {
            Transform panel = FindDescendant(root.transform, panelName);
            AssertSlicedSprite(
                panel.GetComponent<Image>(),
                UltimateRoundedFill);
            Assert.That(panel.GetComponent<Image>().color.a, Is.EqualTo(1f));
            AssertDepthShadow(panel);
            Assert.That(panel.GetComponent<ScrollRect>(), Is.Not.Null);
        }

        private static void AssertAllTextIsBold(Transform root)
        {
            TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
            Assert.That(labels, Is.Not.Empty);
            foreach (TMP_Text label in labels)
            {
                Assert.That(
                    label.fontStyle.HasFlag(FontStyles.Bold),
                    Is.True,
                    $"{GetHierarchyPath(label.transform)} 仍然是细体。");
            }
        }

        private static string GetHierarchyPath(Transform target)
        {
            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }
            return path;
        }

        private static void AssertSlicedSprite(
            Image image,
            string expectedPath)
        {
            Assert.That(image, Is.Not.Null);
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(image.sprite),
                Is.EqualTo(expectedPath));
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
        }

        private static string ResolveExpectedPath(string path)
        {
            return path.StartsWith("Assets/") ? path : ButtonRoot + path;
        }

        private static void AssertDepthShadow(Transform target)
        {
            Shadow shadow = target.GetComponents<Shadow>()
                .FirstOrDefault(component =>
                    component.GetType() == typeof(Shadow));
            Assert.That(shadow, Is.Not.Null);
            Assert.That(shadow.effectDistance.y, Is.LessThan(-1f));
            Assert.That(shadow.effectColor.a, Is.InRange(0.12f, 0.30f));
        }

        private static float ColorLuminance(Color color)
        {
            return 0.2126f * color.r +
                   0.7152f * color.g +
                   0.0722f * color.b;
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
                Transform found = FindDescendant(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
