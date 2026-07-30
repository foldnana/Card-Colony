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
        private const string ComponentRoot =
            "Assets/Layer Lab/GUI Pro-FantasyRPG/ResourcesData/" +
            "Sprites/Component/";
        private const string ButtonRoot =
            ComponentRoot + "Button/";

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
                ButtonRoot +
                "Button_Rectangle_01_Convex_Dark.Png");
            AssertSlicedSprite(
                stats.GetComponent<Image>(),
                ButtonRoot +
                "Button_Rectangle_01_Convex_Dark.Png");
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
            Assert.That(dayText.color.r,
                Is.GreaterThan(dayText.color.b + 0.08f),
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
                ComponentRoot + "Frame/PanelFrame_01_Bg.png");
            AssertDepthShadow(locationView);
            Transform border = FindDescendant(
                locationView,
                "LocationFantasyBorder");
            Assert.That(border, Is.Not.Null);
            AssertSlicedSprite(
                border.GetComponent<Image>(),
                ComponentRoot + "Popup/Popup_01_Border.png");
            Assert.That(border.GetComponent<Image>().raycastTarget, Is.False);

            AssertToggle(
                root,
                "LocationToggle",
                "Button_Rectangle_01_Convex_Blue.Png");
            AssertToggle(
                root,
                "QuestsToggle",
                "Button_Rectangle_01_Convex_Purple.Png");
            AssertToggle(
                root,
                "RecipesToggle",
                "Button_Rectangle_01_Convex_Brown.Png");
            AssertButton(
                root,
                "EnterLocationButton",
                "Button_Rectangle_01_Convex_Green.Png");

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
            Assert.That(description.color.r,
                Is.GreaterThan(description.color.b + 0.05f));
        }

        [Test]
        public void UiRoot_CommonFantasyHudStylesBackpackWithoutReplacingBoard()
        {
            const string backpackBackgroundPath =
                "Assets/StackCraft/Textures/UI/BackpackBackground.png";
            GameObject root =
                AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPath);
            Assert.That(root, Is.Not.Null);

            AssertButton(
                root,
                "BackpackButton",
                "Button_Rectangle_01_Convex_Purple.Png");
            AssertButton(
                root,
                "BackpackCloseButton",
                "Button_Rectangle_01_Convex_Red.Png");
            AssertButton(
                root,
                "BackpackArrangeButton",
                "Button_Rectangle_01_Convex_Yellow.Png");

            Transform background = FindDescendant(
                root.transform,
                "BackpackBackground");
            Image backgroundImage = background.GetComponent<Image>();
            Assert.That(
                AssetDatabase.GetAssetPath(backgroundImage.sprite),
                Is.EqualTo(backpackBackgroundPath),
                "换肤不能替换背包桌面的专用原画。");
            Assert.That(backgroundImage.color, Is.EqualTo(Color.white));
            Assert.That(backgroundImage.preserveAspect, Is.True);

            Transform border = FindDescendant(
                background,
                "BackpackFantasyBorder");
            Assert.That(border, Is.Not.Null);
            AssertSlicedSprite(
                border.GetComponent<Image>(),
                ComponentRoot + "Popup/Popup_01_Border.png");
            Assert.That(border.GetComponent<Image>().raycastTarget, Is.False);

            TMP_Text capacity = FindDescendant(
                root.transform,
                "BackpackCapacityText").GetComponent<TMP_Text>();
            Assert.That(capacity.color.r,
                Is.GreaterThan(capacity.color.b + 0.18f),
                "背包容量文字需要使用金色。");

            Transform backpackRoot =
                FindDescendant(root.transform, "BackpackRoot");
            MonoBehaviour view = backpackRoot
                .GetComponents<MonoBehaviour>()
                .Single(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.BackpackView");
            var serialized = new SerializedObject(view);
            foreach (string property in new[]
                     {
                         "openButton",
                         "openButtonLabel",
                         "tablePanel",
                         "capacityLabel",
                         "closeButton",
                         "arrangeButton",
                         "slotsRoot",
                         "dragLayer"
                     })
            {
                Assert.That(
                    serialized.FindProperty(property)
                        ?.objectReferenceValue,
                    Is.Not.Null,
                    $"换肤不能清除 BackpackView.{property} 绑定。");
            }
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
                ComponentRoot + "Frame/PanelFrame_01_Bg.png");
            Assert.That(infoPanel.GetComponent<Image>().color.a,
                Is.EqualTo(1f));
            AssertDepthShadow(infoPanel);
            Transform infoBorder = FindDescendant(
                infoPanel,
                "InfoPanelFantasyBorder");
            AssertSlicedSprite(
                infoBorder.GetComponent<Image>(),
                ComponentRoot + "Popup/Popup_01_Border.png");
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
                ComponentRoot + "Frame/PanelFrame_01_Bg.png");
            Assert.That(npcPanel.GetComponent<Image>().color.a,
                Is.EqualTo(1f),
                "NPC 内容底板不应继续使用半透明黑色。");
            AssertDepthShadow(npcPanel);
            Transform npcBorder = FindDescendant(
                npcPanel,
                "NpcTradeFantasyBorder");
            AssertSlicedSprite(
                npcBorder.GetComponent<Image>(),
                ComponentRoot + "Popup/Popup_01_Border.png");
            Assert.That(npcBorder.GetComponent<Image>().raycastTarget,
                Is.False);

            Transform scroll =
                FindDescendant(npcPanel, "NpcTradeScrollView");
            AssertSlicedSprite(
                scroll.GetComponent<Image>(),
                ComponentRoot + "Frame/Listframe_01~02_Bg.png");
            Assert.That(scroll.GetComponent<Image>().color.a,
                Is.EqualTo(1f),
                "NPC 列表底板不应继续透出地图。");

            AssertButton(
                root,
                "NpcBuyTabButton",
                "Button_Rectangle_01_Convex_Green.Png");
            AssertButton(
                root,
                "NpcSellTabButton",
                "Button_Rectangle_01_Convex_Brown.Png");
            AssertButton(
                root,
                "NpcActionTabButton",
                "Button_Rectangle_01_Convex_Blue.Png");

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
                ComponentRoot + "Frame/PanelFrame_01_Bg.png");
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
                    ButtonRoot +
                    "Button_Rectangle_01_Convex_Blue.Png");
                Assert.That(
                    returnTransform.GetComponent<Image>().color,
                    Is.EqualTo(Color.white));
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
                ButtonRoot + spriteFileName);
            Assert.That(target.GetComponent<Image>().color,
                Is.EqualTo(Color.white));
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
                ButtonRoot +
                "Button_Rectangle_01_Convex_White_Light.png");
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
                ButtonRoot + spriteFileName);
            Assert.That(target.GetComponent<Image>().color,
                Is.EqualTo(Color.white));
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
                ComponentRoot + "Frame/PanelFrame_01_Bg.png");
            Assert.That(panel.GetComponent<Image>().color.a, Is.EqualTo(1f));
            AssertDepthShadow(panel);
            Assert.That(panel.GetComponent<ScrollRect>(), Is.Not.Null);
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

        private static void AssertDepthShadow(Transform target)
        {
            Shadow shadow = target.GetComponents<Shadow>()
                .FirstOrDefault(component =>
                    component.GetType() == typeof(Shadow));
            Assert.That(shadow, Is.Not.Null);
            Assert.That(shadow.effectDistance.y, Is.LessThan(-1f));
            Assert.That(shadow.effectColor.a, Is.GreaterThanOrEqualTo(0.45f));
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
