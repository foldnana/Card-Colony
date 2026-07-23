using NUnit.Framework;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using CardColony.Gameplay;
using CardColony.TimeSystem;
using CardColony.UnityIntegration;
using CardColony.UnityIntegration.Save;
using CardColony.UnityIntegration.UI;

namespace CardColony.Tests
{
    public class PlayableLoopUnityTests
    {
        [Test]
        public void WorldClockDriver_ExposesPauseSpeedWaitingAndAdvanceControls()
        {
            var gameObject = new GameObject("WorldClockDriver Test");
            try
            {
                var driver = gameObject.AddComponent<WorldClockDriver>();
                var session = new PlayableLoopSession(1f, 360d, 8, 100f);
                driver.Initialize(session);

                driver.SetFastSpeed();
                driver.SetWaiting(true);
                driver.Advance(10f);
                Assert.That(session.Clock.TotalMinutes, Is.EqualTo(400d));

                driver.SetPaused(true);
                driver.Advance(10f);
                Assert.That(session.Clock.TotalMinutes, Is.EqualTo(400d));

                driver.SetNormalSpeed();
                driver.SetPaused(false);
                Assert.That(session.Clock.Speed, Is.EqualTo(WorldClockSpeed.Normal));
                Assert.That(session.Clock.IsWaiting, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RunSnapshotJsonSerializer_RoundTripPreservesActiveAction()
        {
            var session = new PlayableLoopSession(1f, 360d, 8, 100f);
            session.StartExploreWhisperingForest();
            session.Tick(10f);

            string json = RunSnapshotJsonSerializer.Serialize(session.CreateSnapshot());
            RunSnapshot snapshot = RunSnapshotJsonSerializer.Deserialize(json);
            PlayableLoopSession restored = PlayableLoopSession.Restore(snapshot, 1f);

            Assert.That(restored.Clock.TotalMinutes, Is.EqualTo(370d));
            Assert.That(restored.ActiveAction, Is.Not.Null);
            Assert.That(restored.ActiveAction.Type, Is.EqualTo(LoopActionType.ExploreWhisperingForest));
            Assert.That(restored.ActiveAction.ElapsedWorldMinutes, Is.EqualTo(10d));
        }

        [Test]
        public void RunSnapshotFileStore_SaveAndLoad_RestoreAllRunData()
        {
            string fileName = "CardColonyTest-" + System.Guid.NewGuid().ToString("N");
            try
            {
                var session = new PlayableLoopSession(1f, 360d, 8, 100f);
                session.World.SetFlag("quest.herbalist.met", true);
                session.PlayerInventory.Add(PlayableLoopSession.CreateHerbCard(2));
                session.StartExploreWhisperingForest();
                session.Tick(10f);

                RunSnapshotFileStore.Save(fileName, session.CreateSnapshot());
                bool loaded = RunSnapshotFileStore.TryLoad(fileName, out RunSnapshot snapshot);
                PlayableLoopSession restored = PlayableLoopSession.Restore(snapshot, 1f);

                Assert.That(loaded, Is.True);
                Assert.That(restored.Clock.TotalMinutes, Is.EqualTo(370d));
                Assert.That(restored.World.GetFlag("quest.herbalist.met"), Is.True);
                Assert.That(restored.PlayerInventory.GetQuantity(PlayableLoopSession.HerbItemId), Is.EqualTo(2));
                Assert.That(restored.ActiveAction.ElapsedWorldMinutes, Is.EqualTo(10d));
            }
            finally
            {
                RunSnapshotFileStore.Delete(fileName);
            }
        }

        [Test]
        public void RunSnapshotFileStore_OverwriteAndMalformedLoad_AreFailureAware()
        {
            string fileName = "CardColonyTest-" + System.Guid.NewGuid().ToString("N");
            string path = Path.Combine(Application.persistentDataPath, fileName + ".json");
            try
            {
                var first = new PlayableLoopSession(1f, 100d, 8, 100f);
                var second = new PlayableLoopSession(1f, 200d, 8, 100f);

                Assert.That(RunSnapshotFileStore.TrySave(fileName, first.CreateSnapshot(), out string firstError), Is.True, firstError);
                Assert.That(RunSnapshotFileStore.TrySave(fileName, second.CreateSnapshot(), out string secondError), Is.True, secondError);
                Assert.That(RunSnapshotFileStore.TryLoad(fileName, out RunSnapshot loaded, out string loadError), Is.True, loadError);
                Assert.That(loaded.Clock.TotalMinutes, Is.EqualTo(200d));

                File.WriteAllText(path, "{ malformed json");
                Assert.That(RunSnapshotFileStore.TryLoad(fileName, out loaded, out loadError), Is.True, loadError);
                Assert.That(loaded.Clock.TotalMinutes, Is.EqualTo(100d), "损坏主存档时应恢复上一份备份");

                RunSnapshotFileStore.Delete(fileName);
                File.WriteAllText(path, "{ malformed json");
                Assert.That(RunSnapshotFileStore.TryLoad(fileName, out loaded, out loadError), Is.False);
                Assert.That(loaded, Is.Null);
                Assert.That(loadError, Is.Not.Empty);
            }
            finally
            {
                RunSnapshotFileStore.Delete(fileName);
            }
        }

        [UnityTest]
        public IEnumerator GameUiRootPrefab_CenterPassesPointerToNativeCardTable()
        {
            const string prefabPath = "Assets/CardColony/Prefabs/GameUiRoot.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing prefab at {prefabPath}");

            GameObject instance = Object.Instantiate(prefab);
            GameObject temporaryEventSystem = null;
            try
            {
                if (EventSystem.current == null)
                    temporaryEventSystem = new GameObject("Card Colony UI Test EventSystem", typeof(EventSystem));
                WorldClockDriver driver = instance.GetComponentInChildren<WorldClockDriver>(true);
                GameUiPresenter presenter = instance.GetComponentInChildren<GameUiPresenter>(true);
                Assert.That(driver, Is.Not.Null);
                Assert.That(presenter, Is.Not.Null);

                var session = new PlayableLoopSession(1f, 360d, 8, 100f);
                driver.Initialize(session);
                presenter.Bind(driver);
                yield return null;

                GraphicRaycaster raycaster = instance.GetComponentInChildren<GraphicRaycaster>(true);
                Assert.That(raycaster, Is.Not.Null);
                RectTransform canvasRect = (RectTransform)raycaster.transform;
                const float referenceWidth = 1920f;
                const float referenceHeight = 1080f;
                float displayScale = Mathf.Min(
                    Screen.width / referenceWidth,
                    Screen.height / referenceHeight);
                canvasRect.sizeDelta = new Vector2(referenceWidth, referenceHeight);
                canvasRect.localScale = new Vector3(displayScale, displayScale, 1f);
                canvasRect.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
                Canvas.ForceUpdateCanvases();

                var tablePoints = new[]
                {
                    new Vector2(0.50f, 0.50f),
                    new Vector2(0.35f, 0.25f),
                    new Vector2(0.55f, 0.25f),
                    new Vector2(0.75f, 0.30f),
                    new Vector2(0.38f, 0.70f),
                    new Vector2(0.68f, 0.70f)
                };
                foreach (Vector2 normalizedPoint in tablePoints)
                {
                    Vector3 localPoint = new Vector3(
                        (normalizedPoint.x - 0.5f) * referenceWidth,
                        (normalizedPoint.y - 0.5f) * referenceHeight,
                        0f);
                    var pointer = new PointerEventData(EventSystem.current)
                    {
                        position = RectTransformUtility.WorldToScreenPoint(
                            raycaster.eventCamera,
                            canvasRect.TransformPoint(localPoint))
                    };
                    var results = new List<RaycastResult>();
                    raycaster.Raycast(pointer, results);

                    Assert.That(
                        results,
                        Is.Empty,
                        $"桌面点 {normalizedPoint} 不得被 HUD 命中，否则原生 CardController 无法收到拖拽事件");
                }
            }
            finally
            {
                if (temporaryEventSystem != null)
                    Object.DestroyImmediate(temporaryEventSystem);
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GameUiRootPrefab_UsesNativeStackCraftTableWithConceptHud()
        {
            GameObject gameUi = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CardColony/Prefabs/GameUiRoot.prefab");
            GameObject itemCard = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CardColony/Prefabs/ItemCardView.prefab");

            Assert.That(gameUi, Is.Not.Null);
            Assert.That(itemCard, Is.Not.Null);
            Assert.That(
                gameUi.transform.localScale,
                Is.EqualTo(Vector3.one),
                "Prefab 资源根缩放必须为 1，不能依赖 CanvasScaler 在实例化后补救");

            Assert.That(
                FindDescendant(gameUi, "CanvasRoot").localScale,
                Is.EqualTo(Vector3.one),
                "CanvasRoot 缩放必须为 1，否则运行时整套 HUD 不可见");

            Transform worldMap = FindDescendant(gameUi, "WorldMapView");
            Transform forest = FindDescendant(gameUi, "ForestView");
            Transform playerBackpackPanel = FindDescendant(gameUi, "PlayerBackpackPanel");
            Transform questRecipePanel = FindDescendant(gameUi, "QuestRecipePanel");

            Assert.That(worldMap, Is.Not.Null);
            Assert.That(forest, Is.Not.Null);
            Assert.That(playerBackpackPanel, Is.Not.Null);
            Assert.That(questRecipePanel, Is.Not.Null);
            Assert.That(FindDescendant(gameUi, "WhisperingForestCard"), Is.Null);
            Assert.That(FindDescendant(gameUi, "HerbResourceCard"), Is.Null);
            Assert.That(FindDescendant(gameUi, "PlayerCard"), Is.Null);
            Assert.That(FindDescendant(gameUi, "RiverVillageCard"), Is.Null);
            Assert.That(FindDescendant(gameUi, "BackpackSlot1"), Is.Not.Null);
            Assert.That(FindDescendant(gameUi, "BackpackSlot2"), Is.Not.Null);
            Assert.That(FindDescendant(gameUi, "BackpackSlot3"), Is.Not.Null);
            Assert.That(FindDescendant(gameUi, "BackpackSlot4"), Is.Not.Null);
            Assert.That(
                FindDescendant(gameUi, "PlayerTitle").GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("旅行者"));
            Transform playerStats = FindDescendant(gameUi, "PlayerStats");
            Assert.That(playerStats, Is.Not.Null, "角色面板需要独立显示生命与精力");
            Assert.That(playerStats.GetComponent<TMPro.TMP_Text>().text, Does.Contain("生命"));
            Assert.That(playerStats.GetComponent<TMPro.TMP_Text>().text, Does.Contain("\n"));
            Assert.That(
                FindDescendant(gameUi, "FastSpeedButton").GetComponentInChildren<TMPro.TMP_Text>().text,
                Is.EqualTo("四倍"));
            TMPro.TMP_Text interactionHint = FindDescendant(gameUi, "NativeInteractionHint")
                .GetComponent<TMPro.TMP_Text>();
            Assert.That(interactionHint.text, Does.Contain("拖动"));
            Assert.That(interactionHint.text, Does.Contain("叠放"));

            GameObject characterCard = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/Cards/Card_Character.prefab");
            GameObject areaCard = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/Cards/Card_Area.prefab");
            Assert.That(HasComponentNamed(characterCard, "CryingSnow.StackCraft.CardController"), Is.True);
            Assert.That(HasComponentNamed(areaCard, "CryingSnow.StackCraft.CardController"), Is.True);

            Vector2 itemCardSize = itemCard.GetComponent<RectTransform>().sizeDelta;
            Assert.That(itemCardSize.y, Is.GreaterThan(itemCardSize.x), "背包物品应显示为竖向卡牌，而不是横向列表");
        }

        [Test]
        public void GameUiRootPrefab_LeavesCenterForNativeStackCraftCardsAndHidesSaveLoad()
        {
            GameObject gameUi = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CardColony/Prefabs/GameUiRoot.prefab");
            Assert.That(gameUi, Is.Not.Null);

            Transform worldMap = FindDescendant(gameUi, "WorldMapView");
            Transform forest = FindDescendant(gameUi, "ForestView");
            Assert.That(worldMap, Is.Not.Null);
            Assert.That(forest, Is.Not.Null);

            foreach (Transform view in new[] { worldMap, forest })
            {
                Image background = view.GetComponent<Image>();
                Assert.That(background, Is.Not.Null);
                Assert.That(background.raycastTarget, Is.False, $"{view.name} 不得拦截原生卡牌射线");
                Assert.That(background.color.a, Is.Zero, $"{view.name} 不得盖住原生 3D 卡牌桌面");
                Assert.That(
                    view.GetComponentsInChildren<Button>(true),
                    Is.Empty,
                    $"{view.name} 不应再包含按钮式假卡牌");
            }

            Transform saveButton = FindDescendant(gameUi, "SaveButton");
            Transform loadButton = FindDescendant(gameUi, "LoadButton");
            Assert.That(saveButton, Is.Not.Null);
            Assert.That(loadButton, Is.Not.Null);
            Assert.That(saveButton.gameObject.activeSelf, Is.False);
            Assert.That(loadButton.gameObject.activeSelf, Is.False);
            Assert.That(
                FindDescendant(gameUi, "HealingPotionRecipeCard").GetComponent<Button>().interactable,
                Is.False,
                "配方卡只用于查看，不能再通过点击绕过原生卡堆制作");
        }

        [Test]
        public void GameUiRootPrefab_UsesConceptWorldMapHudLayout()
        {
            GameObject gameUi = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CardColony/Prefabs/GameUiRoot.prefab");
            Assert.That(gameUi, Is.Not.Null);

            RectTransform topBar = (RectTransform)FindDescendant(gameUi, "TopBar");
            RectTransform stats = (RectTransform)FindDescendant(gameUi, "TopStatsPanel");
            RectTransform player = (RectTransform)FindDescendant(gameUi, "PlayerBackpackPanel");
            RectTransform journal = (RectTransform)FindDescendant(gameUi, "QuestRecipePanel");

            Assert.That(topBar.anchorMax.x, Is.LessThanOrEqualTo(0.16f), "日期控制应是左上角紧凑块");
            Assert.That(stats, Is.Not.Null, "概念图左上角需要独立的状态统计块");
            Assert.That(player.anchorMax.x, Is.LessThanOrEqualTo(0.23f));
            Assert.That(journal.anchorMin.x, Is.GreaterThanOrEqualTo(0.80f));

            Assert.That(FindDescendant(gameUi, "InteractTabButton"), Is.Not.Null);
            Assert.That(FindDescendant(gameUi, "CampTabButton"), Is.Not.Null);
            Assert.That(FindDescendant(gameUi, "MapTabButton"), Is.Not.Null);
        }

        [Test]
        public void ItemCardView_UsesMultilineChineseCopyInsideCompactCard()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CardColony/Prefabs/ItemCardView.prefab");
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                ItemCardView view = instance.GetComponent<ItemCardView>();
                view.Bind(PlayableLoopSession.CreateHerbCard(3), null, null);

                Assert.That(
                    FindDescendant(instance, "Title").GetComponent<TMPro.TMP_Text>().text,
                    Does.Contain("\n"));
                Assert.That(
                    FindDescendant(instance, "Details").GetComponent<TMPro.TMP_Text>().text,
                    Does.Contain("\n"));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GameUiPresenter_LoadingEarlierSave_ReturnsToLockedWorldMapView()
        {
            string fileName = "CardColonyUiViewTest-" + System.Guid.NewGuid().ToString("N");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CardColony/Prefabs/GameUiRoot.prefab");
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                WorldClockDriver driver = instance.GetComponentInChildren<WorldClockDriver>(true);
                GameUiPresenter presenter = instance.GetComponentInChildren<GameUiPresenter>(true);
                var serializedDriver = new SerializedObject(driver);
                serializedDriver.FindProperty("saveFileName").stringValue = fileName;
                serializedDriver.ApplyModifiedPropertiesWithoutUndo();

                driver.Initialize(new PlayableLoopSession(1f, 360d, 8, 100f));
                presenter.Bind(driver);
                FindDescendant(instance, "SaveButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

                presenter.ExploreButton.onClick.Invoke();
                driver.Advance(PlayableLoopSession.ExploreDurationMinutes);
                Assert.That(FindDescendant(instance, "ForestView").gameObject.activeSelf, Is.True);

                FindDescendant(instance, "LoadButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

                Assert.That(
                    driver.Session.World.GetOrCreateLocation(PlayableLoopSession.ForestLocationId).IsDiscovered,
                    Is.False);
                Assert.That(FindDescendant(instance, "WorldMapView").gameObject.activeSelf, Is.True);
                Assert.That(FindDescendant(instance, "ForestView").gameObject.activeSelf, Is.False);
            }
            finally
            {
                RunSnapshotFileStore.Delete(fileName);
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void MainScene_UsesOnlyOriginalStackCraftUiRoot()
        {
            const string scenePath = "Assets/StackCraft/Scenes/Main.unity";
            const string originalUiRootPath = "Assets/StackCraft/Prefabs/UI/UIRoot.prefab";
            const string duplicateConceptUiPath = "Assets/CardColony/Prefabs/GameUiRoot.prefab";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            string[] rootPrefabPaths = scene.GetRootGameObjects()
                .Select(PrefabUtility.GetCorrespondingObjectFromOriginalSource)
                .Where(source => source != null)
                .Select(AssetDatabase.GetAssetPath)
                .ToArray();

            Assert.That(
                rootPrefabPaths.Count(path => path == originalUiRootPath),
                Is.EqualTo(1),
                "Main 应且只应保留一个原项目 UIRoot");
            Assert.That(
                rootPrefabPaths,
                Has.None.EqualTo(duplicateConceptUiPath),
                "Main 只能使用原项目 UIRoot；额外的全屏概念 HUD 会重复绘制日期、状态和任务面板");
        }

        [Test]
        public void OriginalUiRoot_HasFixedWorldMapLocationSidebarWithoutRouteButton()
        {
            GameObject uiRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Assert.That(uiRoot, Is.Not.Null);

            Transform locationToggle = FindDescendant(uiRoot, "LocationToggle");
            Transform locationView = FindDescendant(uiRoot, "LocationView");
            Transform enterButton = FindDescendant(uiRoot, "EnterLocationButton");
            Assert.That(locationToggle, Is.Not.Null, "原生右侧栏需要新增地点页签");
            Assert.That(locationView, Is.Not.Null, "地点详情必须作为原生右侧栏中的固定页面");
            Assert.That(enterButton, Is.Not.Null);
            Assert.That(FindDescendant(uiRoot, "LocationExploration"), Is.Null,
                "当前地点栏不应显示探索进度文字");
            Assert.That(FindDescendant(uiRoot, "LocationExplorationBar"), Is.Null,
                "当前地点栏不应显示探索进度条");
            Assert.That(FindDescendant(uiRoot, "ViewRouteButton"), Is.Null,
                "当前版本不应显示查看路线按钮");
            Assert.That(
                locationToggle.GetComponentInChildren<TMPro.TMP_Text>(true).text,
                Is.EqualTo("地点"));
            Assert.That(
                enterButton.GetComponentInChildren<TMPro.TMP_Text>(true).text,
                Is.EqualTo("进入地点"));
            Assert.That(
                locationView.GetComponents<MonoBehaviour>()
                    .Any(component => component.GetType().FullName ==
                        "CryingSnow.StackCraft.WorldMapLocationView"),
                Is.True,
                "地点页需要由 WorldMapLocationView 绑定选中的原生地点卡");
        }

        [Test]
        public void OriginalUiRoot_HasPartyStatusAndEightSlotBackpackTabletop()
        {
            GameObject uiRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Assert.That(uiRoot, Is.Not.Null);

            Transform panel = FindDescendant(uiRoot, "WorldMapPartyStatusPanel");
            Assert.That(panel, Is.Not.Null, "世界地图左下角需要固定的小队状态栏");
            Assert.That(
                panel.GetComponents<MonoBehaviour>().Any(component =>
                    component.GetType().FullName ==
                    "CryingSnow.StackCraft.WorldMapPartyStatusView"),
                Is.True);
            Assert.That(FindDescendant(panel.gameObject, "PartyPortrait"), Is.Not.Null);
            Assert.That(FindDescendant(panel.gameObject, "PartyName"), Is.Not.Null);
            Assert.That(FindDescendant(panel.gameObject, "PartyHealthText"), Is.Not.Null);
            Assert.That(FindDescendant(panel.gameObject, "PartyHealthBar"), Is.Not.Null);
            Assert.That(FindDescendant(panel.gameObject, "PartyLocationText"), Is.Not.Null);
            Assert.That(FindDescendant(panel.gameObject, "PartyMembersText"), Is.Not.Null);
            Assert.That(FindDescendant(panel.gameObject, "PartyStateText"), Is.Not.Null);
            Transform backpackRoot = FindDescendant(uiRoot, "BackpackRoot");
            Assert.That(backpackRoot, Is.Not.Null, "正式 UIRoot 需要常驻的背包入口和小桌面");
            Assert.That(
                backpackRoot.GetComponents<MonoBehaviour>().Any(component =>
                    component.GetType().FullName == "CryingSnow.StackCraft.BackpackView"),
                Is.True);
            Assert.That(FindDescendant(backpackRoot.gameObject, "BackpackButton"), Is.Not.Null);
            Assert.That(FindDescendant(backpackRoot.gameObject, "BackpackTablePanel"), Is.Not.Null);
            Transform scrollViewport = FindDescendant(backpackRoot.gameObject, "BackpackScrollViewport");
            Assert.That(scrollViewport, Is.Not.Null);
            Assert.That(scrollViewport.GetComponent<ScrollRect>(), Is.Not.Null);
            Assert.That(scrollViewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(FindDescendant(backpackRoot.gameObject, "BackpackCapacityText"), Is.Not.Null);
            Assert.That(FindDescendant(backpackRoot.gameObject, "BackpackCloseButton"), Is.Not.Null);
            Assert.That(
                backpackRoot.GetComponentsInChildren<Transform>(true)
                    .Count(child => child.name.StartsWith("BackpackSlot") &&
                        child.name != "BackpackSlots"),
                Is.EqualTo(8));

            RectTransform rect = panel.GetComponent<RectTransform>();
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.zero));
            Assert.That(rect.pivot, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchoredPosition.y, Is.GreaterThanOrEqualTo(100f),
                "小队信息框需要位于背包入口上方，避免两个常驻 UI 相互遮挡");
            Assert.That(rect.sizeDelta.x, Is.GreaterThanOrEqualTo(400f));
            Assert.That(rect.sizeDelta.y, Is.GreaterThanOrEqualTo(300f));
        }

        [Test]
        public void OriginalUiRoot_BackpackTableUsesDedicatedBagBackground()
        {
            const string backgroundPath =
                "Assets/StackCraft/Textures/UI/BackpackBackground.png";
            Sprite expectedBackground = AssetDatabase.LoadAssetAtPath<Sprite>(backgroundPath);
            Assert.That(expectedBackground, Is.Not.Null,
                "背包图片需要作为独立的 Sprite 资源导入项目");

            GameObject uiRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Transform table = FindDescendant(uiRoot, "BackpackTablePanel");
            Assert.That(table, Is.Not.Null);

            Image tableImage = table.GetComponent<Image>();
            Assert.That(tableImage, Is.Not.Null);
            Assert.That(tableImage.enabled, Is.False,
                "旧的矩形面板图像需要关闭，避免挡住独立背包背景");

            Transform background = FindDescendant(table.gameObject, "BackpackBackground");
            Assert.That(background, Is.Not.Null,
                "背包背景需要作为独立子物体，以便单独调整大小和位置");
            Image backgroundImage = background.GetComponent<Image>();
            Assert.That(backgroundImage, Is.Not.Null);
            Assert.That(backgroundImage.sprite, Is.EqualTo(expectedBackground),
                "独立背包背景需要引用新的背包图片");
            Assert.That(backgroundImage.color, Is.EqualTo(Color.white),
                "背景图不应继续叠加旧的深色染色");
            Assert.That(backgroundImage.preserveAspect, Is.True,
                "背包背景需要保持原图比例，避免皮包边框被拉伸");
        }

        [Test]
        public void BackpackLayout_IsIdenticalAcrossWorldMapAndLocationScenes()
        {
            string expectedLayout = CaptureBackpackLayout(
                "Assets/StackCraft/Scenes/Main.unity");

            foreach (string scenePath in new[]
                     {
                         "Assets/StackCraft/Scenes/Location.unity",
                         "Assets/StackCraft/Scenes/Island.unity"
                     })
            {
                Assert.That(
                    CaptureBackpackLayout(scenePath),
                    Is.EqualTo(expectedLayout),
                    $"{scenePath} 必须使用和世界地图完全相同的背包布局");
            }
        }

        [Test]
        public void BackpackView_CreatesRaisedThreeDimensionalBoard()
        {
            System.Type boardType = FindType("CryingSnow.StackCraft.BackpackBoardView");
            Assert.That(boardType, Is.Not.Null,
                "背包打开后应使用独立的三维小桌面，而不是把物品转换成二维卡片");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            GameObject uiInstance = Object.Instantiate(prefab);
            try
            {
                Transform root = FindDescendant(uiInstance, "BackpackRoot");
                Component view = root.GetComponents<MonoBehaviour>().First(component =>
                    component.GetType().FullName == "CryingSnow.StackCraft.BackpackView");
                PropertyInfo boardProperty = view.GetType().GetProperty("Board3D");
                Assert.That(boardProperty, Is.Not.Null);

                Component board = boardProperty.GetValue(view) as Component;
                if (board == null)
                {
                    view.GetType().GetMethod(
                            "EnsureBoard3D",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(view, null);
                    board = boardProperty.GetValue(view) as Component;
                }
                Assert.That(board, Is.Not.Null);
                Assert.That(board.transform, Is.Not.TypeOf<RectTransform>(),
                    "三维背包桌面不能继续依附在屏幕空间 RectTransform 中");
                Assert.That(
                    board.GetComponentsInChildren<MeshRenderer>(true),
                    Is.Not.Empty);
                Assert.That(
                    board.GetComponentsInChildren<BoxCollider>(true),
                    Is.Not.Empty);
                Assert.That(
                    (float)boardType.GetProperty("SurfaceHeight").GetValue(board),
                    Is.GreaterThan(0f),
                    "背包桌面需要高于地图桌面，形成明确的双层桌面效果");
            }
            finally
            {
                Object.DestroyImmediate(uiInstance);
            }
        }

        [Test]
        public void BackpackBoard_RebuildsEntriesAsNativeThreeDimensionalCardProxies()
        {
            System.Type boardType = FindType("CryingSnow.StackCraft.BackpackBoardView");
            System.Type proxyType = FindType("CryingSnow.StackCraft.BackpackCardProxy");
            System.Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            System.Type dropHandlerType = FindType("CryingSnow.StackCraft.ICardDropHandler");

            Assert.That(proxyType, Is.Not.Null,
                "背包桌面中的物品仍应是原生三维卡牌，并通过代理组件保留背包归属");
            Assert.That(dropHandlerType.IsAssignableFrom(proxyType), Is.True,
                "背包三维卡需要接管放下行为，才能在背包桌面和地图桌面间转移");
            Assert.That(
                boardType.GetMethod(
                    "Rebuild",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { backpackType },
                    null),
                Is.Not.Null);
            Assert.That(
                boardType.GetMethod(
                    "ContainsScreenPoint",
                    BindingFlags.Public | BindingFlags.Instance),
                Is.Not.Null,
                "世界卡牌需要通过三维桌面碰撞范围判断是否放入背包");
        }

        [Test]
        public void BackpackCardProxy_ProvidesDragHeightAboveRaisedSurface()
        {
            System.Type providerType =
                FindType("CryingSnow.StackCraft.ICardDragHeightProvider");
            System.Type proxyType = FindType("CryingSnow.StackCraft.BackpackCardProxy");
            Assert.That(providerType, Is.Not.Null,
                "高架桌面上的卡牌需要覆盖默认地面拖拽高度");
            Assert.That(providerType.IsAssignableFrom(proxyType), Is.True);
            Assert.That(
                FindType("CryingSnow.StackCraft.CardController").GetMethod(
                    "ResolveDragHeight",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "CardController 应从当前卡牌组件解析拖拽平面高度");
        }

        [Test]
        public void BackpackView_ExposesRaisedDragHeightForCardsEnteringBoard()
        {
            System.Type backpackViewType = FindType("CryingSnow.StackCraft.BackpackView");
            Assert.That(
                backpackViewType.GetMethod(
                    "TryGetStorageDragHeight",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "地图卡拖进较高的背包桌面时需要先抬高，不能从桌面模型下方穿过");
        }

        [Test]
        public void BackpackData_PersistsFreeTablePlacementWithoutCompactingItIntoSlots()
        {
            System.Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            object backpack = System.Activator.CreateInstance(backpackType);
            object cardData = System.Activator.CreateInstance(cardDataType);
            cardDataType.GetField("Id").SetValue(cardData, "egg");
            object[] addArguments = { cardData, null };
            backpackType.GetMethod("TryAdd").Invoke(backpack, addArguments);
            object entry = addArguments[1];

            MethodInfo setPlacement = backpackType.GetMethod("TrySetTablePlacement");
            Assert.That(setPlacement, Is.Not.Null,
                "背包卡牌需要保存自由桌面坐标和堆叠归属，不能再只保存格子编号");
            Assert.That(
                setPlacement.Invoke(
                    backpack,
                    new object[] { entry.GetType().GetField("InstanceId").GetValue(entry), -1.35f, 0.72f, "herb-stack", 2 }),
                Is.True);

            backpackType.GetMethod("Compact").Invoke(backpack, null);
            Assert.That(entry.GetType().GetField("HasTablePosition").GetValue(entry), Is.True);
            Assert.That(entry.GetType().GetField("TablePositionX").GetValue(entry), Is.EqualTo(-1.35f));
            Assert.That(entry.GetType().GetField("TablePositionZ").GetValue(entry), Is.EqualTo(0.72f));
            Assert.That(entry.GetType().GetField("TableStackId").GetValue(entry), Is.EqualTo("herb-stack"));
            Assert.That(entry.GetType().GetField("TableStackOrder").GetValue(entry), Is.EqualTo(2));
        }

        [Test]
        public void BackpackBoard_ExposesFreePlacementInsteadOfSlotPlacement()
        {
            System.Type boardType = FindType("CryingSnow.StackCraft.BackpackBoardView");
            System.Type proxyType = FindType("CryingSnow.StackCraft.BackpackCardProxy");
            System.Type entryType = FindType("CryingSnow.StackCraft.BackpackEntryData");

            Assert.That(
                boardType.GetMethod(
                    "PlaceOnTable",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { proxyType, typeof(Vector3) },
                    null),
                Is.Not.Null,
                "背包桌面应按落点自由摆放，并允许靠近的卡牌堆叠");
            Assert.That(
                boardType.GetMethod(
                    "GetTableWorldPosition",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { entryType },
                    null),
                Is.Not.Null,
                "恢复背包时应使用已保存的自由桌面坐标，而不是四列格子布局");
            Assert.That(
                boardType.GetMethod(
                    "CanStackTogether",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "自由桌面的堆叠仍必须复用原项目的 CanStack 规则，不能按距离强制合并不兼容卡牌");
            Assert.That(
                boardType.GetMethod(
                    "MergeVisualStacks",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Not.Null,
                "背包合堆只能改变视觉卡堆，不能触发宝箱等世界 IOnStackable 行为");
        }

        [Test]
        public void BackpackOverlayLayer_IsReservedForTheDedicatedCamera()
        {
            Assert.That(
                LayerMask.NameToLayer("BackpackOverlay"),
                Is.EqualTo(30),
                "The backpack needs an isolated layer so the world camera cannot render it.");
        }

        [Test]
        public void BackpackView_CreatesADedicatedOverlayCamera()
        {
            System.Type viewType = FindType("CryingSnow.StackCraft.BackpackView");
            Camera previousMain = Camera.main;
            string previousTag = previousMain != null
                ? previousMain.gameObject.tag
                : null;
            if (previousMain != null)
                previousMain.gameObject.tag = "Untagged";

            var mainObject = new GameObject(
                "BackpackOverlayMainCameraTest",
                typeof(Camera),
                typeof(PhysicsRaycaster));
            mainObject.tag = "MainCamera";
            Camera mainCamera = mainObject.GetComponent<Camera>();
            mainCamera.depth = -1f;
            var viewObject = new GameObject("BackpackOverlayViewTest");

            try
            {
                Component existing =
                    viewType.GetProperty("Instance").GetValue(null) as Component;
                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);

                Component view = viewObject.AddComponent(viewType);
                viewType.GetMethod(
                        "EnsureBoard3D",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, null);
                PropertyInfo overlayProperty = viewType.GetProperty("OverlayCamera");
                Assert.That(overlayProperty, Is.Not.Null);
                Camera overlay = overlayProperty.GetValue(view) as Camera;

                Assert.That(overlay, Is.Not.Null);
                Assert.That(overlay, Is.Not.SameAs(mainCamera));
                Assert.That(overlay.depth, Is.GreaterThan(mainCamera.depth));
                Assert.That(
                    overlay.cullingMask,
                    Is.EqualTo(1 << 30));
                Assert.That(
                    mainCamera.cullingMask & (1 << 30),
                    Is.Zero);
                Assert.That(
                    overlay.clearFlags,
                    Is.EqualTo(CameraClearFlags.Depth));
                Assert.That(
                    overlay.GetComponent<PhysicsRaycaster>(),
                    Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(viewObject);
                Object.DestroyImmediate(mainObject);
                if (previousMain != null)
                    previousMain.gameObject.tag = previousTag;
            }
        }

        [Test]
        public void BackpackBoard_EmptySurfaceOwnsTheDragGesture()
        {
            System.Type boardType = FindType("CryingSnow.StackCraft.BackpackBoardView");
            System.Type handleType =
                FindType("CryingSnow.StackCraft.BackpackBoardDragSurface");
            Assert.That(handleType, Is.Not.Null);
            Assert.That(typeof(IBeginDragHandler).IsAssignableFrom(handleType), Is.True);
            Assert.That(typeof(IDragHandler).IsAssignableFrom(handleType), Is.True);
            Assert.That(typeof(IEndDragHandler).IsAssignableFrom(handleType), Is.True);

            var boardObject = new GameObject("BackpackSurfaceDragTest");
            try
            {
                Component board = boardObject.AddComponent(boardType);
                boardType.GetMethod(
                        "BuildVisuals",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(board, null);
                Transform surface = boardObject
                    .GetComponentsInChildren<Transform>(true)
                    .Single(child => child.name == "Backpack3DSurface");
                Assert.That(surface.GetComponent(handleType), Is.Not.Null,
                    "Only the exposed empty board surface should move the backpack.");
            }
            finally
            {
                Object.DestroyImmediate(boardObject);
            }
        }

        [Test]
        public void CardController_ExposesTheCrossCameraBackpackDragBridge()
        {
            System.Type controllerType =
                FindType("CryingSnow.StackCraft.CardController");
            Assert.That(
                controllerType.GetMethod(
                    "TryResolveBackpackDragPosition",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "Cards need a screen-space bridge when crossing between world and overlay cameras.");
        }

        [Test]
        public void CardController_KeepsCrossCameraBridgeActiveUntilDrop()
        {
            System.Type controllerType =
                FindType("CryingSnow.StackCraft.CardController");
            Assert.That(
                controllerType.GetField(
                    "_backpackBridgeActive",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "After leaving the overlay, a dragged card must keep using its screen-space offset until it is dropped.");
        }

        [Test]
        public void CrossCameraBridge_ExposesClosedBackpackAndWholeStackSafeguards()
        {
            System.Type viewType = FindType("CryingSnow.StackCraft.BackpackView");
            Assert.That(
                viewType.GetMethod(
                    "TryResolveClosedBackpackDrag",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "Closing the backpack during a drag must return the active stack to the world camera.");
            Assert.That(
                viewType.GetMethod(
                    "SnapStackAfterRenderSpaceChange",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Not.Null,
                "Every card in a stack must cross the 10,000-unit camera-space gap instantly.");

            System.Type controllerType =
                FindType("CryingSnow.StackCraft.CardController");
            Assert.That(
                controllerType.GetMethod(
                    "ResolveFinalDropPosition",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "A failed backpack store must use the restored world target instead of a stale overlay transform.");
        }

        [Test]
        public void BackpackOverlay_DragsTheCameraWhileKeepingTheBoardFixed()
        {
            System.Type viewType = FindType("CryingSnow.StackCraft.BackpackView");
            FieldInfo distance = viewType.GetField(
                "OverlayCameraDistance",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(distance, Is.Not.Null);
            Assert.That(
                (float)distance.GetRawConstantValue(),
                Is.GreaterThanOrEqualTo(15f),
                "The backpack overlay should occupy less screen space than the initial close camera.");

            System.Type boardType =
                FindType("CryingSnow.StackCraft.BackpackBoardView");
            Assert.That(
                boardType.GetMethod(
                    "SynchronizeStackTargetsToVisuals",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "The opening tween must not leave card targets at the hidden animation position.");
            MethodInfo updateCameraMotion = boardType.GetMethod(
                "UpdateSurfaceCameraMotion",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(updateCameraMotion, Is.Not.Null,
                "Backpack surface dragging should smooth a dedicated camera target instead of moving the card table.");
            DefaultExecutionOrder executionOrder =
                boardType.GetCustomAttribute<DefaultExecutionOrder>();
            Assert.That(executionOrder, Is.Not.Null);
            Assert.That(executionOrder.order, Is.LessThan(0),
                "The overlay camera must move before the close-button LateUpdate projects the board corner.");

            GameObject cameraObject = new("Backpack Drag Camera");
            GameObject boardObject = new("Backpack Drag Board");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 10f;
                camera.transform.position = new Vector3(0f, 10f, 0f);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                Component board = boardObject.AddComponent(boardType);
                boardType.GetMethod("ConfigureOverlay")?.Invoke(
                    board,
                    new object[] { camera, 30 });

                Vector3 screenCenter = camera.WorldToScreenPoint(
                    boardObject.transform.position);
                Vector2 start = new(screenCenter.x, screenCenter.y);
                Vector2 before = (Vector2)boardType
                    .GetProperty("ViewportAnchor")
                    ?.GetValue(board);
                Vector3 boardPositionBefore = boardObject.transform.position;
                Vector3 cameraPositionBefore = camera.transform.position;

                boardType.GetMethod(
                    "BeginSurfaceDrag",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(board, new object[] { start });
                boardType.GetMethod(
                    "DragSurface",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(board, new object[] { start + Vector2.right * 40f });
                updateCameraMotion.Invoke(board, new object[] { 1f });

                Vector2 after = (Vector2)boardType
                    .GetProperty("ViewportAnchor")
                    ?.GetValue(board);
                Vector3 projectedAfter = camera.WorldToScreenPoint(
                    boardObject.transform.position);
                Assert.That(
                    boardObject.transform.position,
                    Is.EqualTo(boardPositionBefore),
                    "Dragging the backpack must leave the board and its card children in stable world coordinates.");
                Assert.That(
                    camera.transform.position.x,
                    Is.LessThan(cameraPositionBefore.x),
                    "Dragging the backpack to the right should move its overlay camera to the left.");
                Assert.That(
                    after.x,
                    Is.GreaterThan(before.x + 0.001f),
                    "The requested viewport anchor should follow the pointer immediately.");
                Assert.That(
                    projectedAfter.x,
                    Is.GreaterThan(start.x + 1f),
                    "The fixed board should appear to move right after the inverse camera pan.");
            }
            finally
            {
                Object.DestroyImmediate(boardObject);
                Object.DestroyImmediate(cameraObject);
            }

            System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            Assert.That(
                stackType.GetMethod(
                    "SynchronizeTargetWithParentMotion",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null,
                "Moving the backpack should update only the logical stack target because child card transforms already follow their parent.");
            Assert.That(
                stackType.GetMethod(
                    "StopMovementForParentDrag",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null,
                "Starting a board drag must stop card-local tweens and damping once so they cannot pull cards away from the moving parent.");
            Assert.That(
                boardType.GetMethod(
                    "StopStackMotionBeforeSurfaceDrag",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "The backpack must settle each distinct stack before the surface drag takes ownership.");
        }

        [Test]
        public void BackpackOverlay_ReopensFromItsFixedBoardPosition()
        {
            System.Type boardType =
                FindType("CryingSnow.StackCraft.BackpackBoardView");
            GameObject cameraObject = new("Backpack Reopen Camera");
            GameObject boardObject = new("Backpack Reopen Board");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 10f;
                camera.transform.position = new Vector3(0f, 10f, 0f);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                boardObject.transform.position =
                    new Vector3(10000f, 0f, 10000f);
                Component board = boardObject.AddComponent(boardType);
                boardType.GetMethod("ConfigureOverlay")?.Invoke(
                    board,
                    new object[] { camera, 30 });
                Vector3 fixedPosition = boardObject.transform.position;

                boardObject.transform.position =
                    fixedPosition + Vector3.down * 1.2f;
                boardType.GetMethod("SetVisible")?.Invoke(
                    board,
                    new object[] { true });

                Assert.That(
                    boardObject.transform.position.x,
                    Is.EqualTo(fixedPosition.x).Within(0.001f));
                Assert.That(
                    boardObject.transform.position.z,
                    Is.EqualTo(fixedPosition.z).Within(0.001f));
                Assert.That(
                    boardObject.transform.position.y,
                    Is.EqualTo(fixedPosition.y - 1.2f).Within(0.001f),
                    "Reopening should start one transition below the fixed board position, not accumulate the previous close offset.");
            }
            finally
            {
                Object.DestroyImmediate(boardObject);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void BackpackOverlay_DragsEveryCardInAStackAsOneRigidLayout()
        {
            System.Type viewType = FindType("CryingSnow.StackCraft.BackpackView");
            Assert.That(
                viewType.GetMethod(
                    "ShouldUseRigidStackDrag",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "Backpack stacks must retain their normal StackStep spacing while dragged.");

            System.Type controllerType =
                FindType("CryingSnow.StackCraft.CardController");
            Assert.That(
                controllerType.GetMethod(
                    "MoveDraggedStack",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "CardController needs one motion path that can disable trailing-card sway on the backpack.");
        }

        [Test]
        public void BackpackService_CanAppendAFirstDropToAnExistingTableStack()
        {
            System.Type serviceType =
                FindType("CryingSnow.StackCraft.BackpackService");
            System.Type backpackType =
                FindType("CryingSnow.StackCraft.BackpackData");
            System.Type cardType =
                FindType("CryingSnow.StackCraft.CardInstance");
            MethodInfo store = serviceType.GetMethod(
                "TryStoreAtTablePosition",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    cardType,
                    backpackType,
                    typeof(Vector2),
                    typeof(string),
                    typeof(int)
                },
                null);
            Assert.That(store, Is.Not.Null,
                "The first world-to-backpack drop must be able to reuse a compatible table stack id.");

            object backpack = System.Activator.CreateInstance(backpackType);
            System.Type cardDataType =
                FindType("CryingSnow.StackCraft.CardData");
            object storedData = System.Activator.CreateInstance(cardDataType);
            storedData.GetType().GetField("Id").SetValue(storedData, "egg");
            object[] add = { storedData, null };
            backpackType.GetMethod("TryAdd").Invoke(backpack, add);
            object storedEntry = add[1];
            string storedId = (string)storedEntry.GetType()
                .GetField("InstanceId").GetValue(storedEntry);
            backpackType.GetMethod("TrySetTablePlacement").Invoke(
                backpack,
                new object[] { storedId, 0.25f, -0.2f, "existing-stack", 0 });

            Object eggDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Egg.asset");
            Component egg = CreateUninitializedCard(
                eggDefinition,
                "First Drop Existing Stack Egg");
            try
            {
                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex(
                        "Destroy may not be called from edit mode!"));
                Assert.That(
                    store.Invoke(
                        null,
                        new object[]
                        {
                            egg,
                            backpack,
                            new Vector2(0.25f, -0.2f),
                            "existing-stack",
                            1
                        }),
                    Is.True);

                IList entries = (IList)backpackType.GetField("Entries")
                    .GetValue(backpack);
                Assert.That(entries.Count, Is.EqualTo(2));
                object appended = entries.Cast<object>()
                    .Single(entry => !ReferenceEquals(entry, storedEntry));
                Assert.That(
                    appended.GetType().GetField("TableStackId")
                        .GetValue(appended),
                    Is.EqualTo("existing-stack"));
                Assert.That(
                    appended.GetType().GetField("TableStackOrder")
                        .GetValue(appended),
                    Is.EqualTo(1));
            }
            finally
            {
                DestroyTestCard(egg);
            }

            System.Type boardType =
                FindType("CryingSnow.StackCraft.BackpackBoardView");
            Assert.That(
                boardType.GetMethod(
                    "TryResolveStoragePlacement",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "The board must select a compatible nearby table stack before storage rebuilds its visuals.");
        }

        [Test]
        public void LocationNpcActivity_StopsBeforeWalkingIntoAnotherNpc()
        {
            System.Type managerType =
                FindType("CryingSnow.StackCraft.CardManager");
            System.Type activityType =
                FindType("CryingSnow.StackCraft.LocationNpcActivity");
            Object chiefDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_VillageChief.asset");
            Component first = CreateUninitializedCard(
                chiefDefinition,
                "Moving Collision Safe NPC");
            Component second = CreateUninitializedCard(
                chiefDefinition,
                "Stationary Collision Safe NPC");
            PropertyInfo managerInstance = managerType.GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.Static);
            Component previousManager =
                managerInstance.GetValue(null) as Component;
            managerInstance.SetValue(null, null);
            MonoBehaviour manager = (MonoBehaviour)new GameObject(
                    "NPC Collision Test CardManager")
                .AddComponent(managerType);
            managerInstance.SetValue(null, manager);

            try
            {
                first.GetType().GetProperty("Size").SetValue(first, Vector2.one);
                second.GetType().GetProperty("Size").SetValue(second, Vector2.one);
                SetTestCardStackPosition(first, Vector3.zero);
                SetTestCardStackPosition(second, new Vector3(0.8f, 0f, 0f));
                object firstStack =
                    first.GetType().GetProperty("Stack").GetValue(first);
                object secondStack =
                    second.GetType().GetProperty("Stack").GetValue(second);
                managerType.GetMethod("RegisterStack").Invoke(
                    manager,
                    new[] { firstStack });
                managerType.GetMethod("RegisterStack").Invoke(
                    manager,
                    new[] { secondStack });

                Component firstActivity =
                    first.gameObject.AddComponent(activityType);
                Component secondActivity =
                    second.gameObject.AddComponent(activityType);
                activityType.GetMethod("Configure").Invoke(
                    firstActivity,
                    new object[]
                    {
                        first,
                        Vector3.zero,
                        3f,
                        1f,
                        new Vector2(10f, 10f)
                    });
                activityType.GetMethod("Configure").Invoke(
                    secondActivity,
                    new object[]
                    {
                        second,
                        new Vector3(0.8f, 0f, 0f),
                        0.1f,
                        0.1f,
                        new Vector2(10f, 10f)
                    });
                activityType.GetMethod("SetDestination").Invoke(
                    firstActivity,
                    new object[] { new Vector3(2f, 0f, 0f) });

                Assert.That(
                    ((IEnumerable)managerType.GetProperty("AllCards")
                        .GetValue(manager)).Cast<object>().Count(),
                    Is.EqualTo(2),
                    "The collision test must register both NPC stacks.");
                Assert.That(
                    second.GetComponent(activityType),
                    Is.Not.Null,
                    "The stationary card must be recognized as an ambient NPC.");
                Assert.That(
                    firstStack.GetType().GetProperty("Width")
                        .GetValue(firstStack),
                    Is.EqualTo(1f));
                Assert.That(
                    secondStack.GetType().GetProperty("Width")
                        .GetValue(secondStack),
                    Is.EqualTo(1f));
                Assert.That(
                    secondStack.GetType().GetProperty("TargetPosition")
                        .GetValue(secondStack),
                    Is.EqualTo(new Vector3(0.8f, 0f, 0f)));
                Assert.That(
                    FindType("CryingSnow.StackCraft.CardPhysicsSolver")
                        .GetMethod(
                            "WouldOverlapAt",
                            BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(
                            null,
                            new object[]
                            {
                                firstStack,
                                new Vector3(1f, 0f, 0f),
                                secondStack,
                                0.04f
                            }),
                    Is.True,
                    "The shared footprint helper must detect the overlap.");
                Assert.That(
                    activityType.GetMethod(
                            "IsNpcMovementBlocked",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(
                            firstActivity,
                            new object[] { new Vector3(1f, 0f, 0f) }),
                    Is.True,
                    "The candidate step overlaps the stationary NPC footprint.");

                activityType.GetMethod("Tick").Invoke(
                    firstActivity,
                    new object[] { 1f });

                Vector3 firstPosition = (Vector3)firstStack.GetType()
                    .GetProperty("TargetPosition").GetValue(firstStack);
                Assert.That(
                    firstPosition,
                    Is.EqualTo(Vector3.zero),
                    "The ambient NPC should wait instead of entering a push-pull collision with another NPC.");
                Assert.That(
                    activityType.GetProperty("State").GetValue(firstActivity)
                        .ToString(),
                    Is.EqualTo("Idle"));

                activityType.GetMethod("SetDestination").Invoke(
                    firstActivity,
                    new object[] { new Vector3(-2f, 0f, 0f) });
                activityType.GetMethod("Tick").Invoke(
                    firstActivity,
                    new object[] { 0.1f });
                Vector3 escapingPosition = (Vector3)firstStack.GetType()
                    .GetProperty("TargetPosition").GetValue(firstStack);
                Assert.That(
                    escapingPosition.x,
                    Is.LessThan(0f),
                    "An NPC that already overlaps another NPC must still be allowed to move away smoothly.");

                SetTestCardStackPosition(first, Vector3.zero);
                SetTestCardStackPosition(second, Vector3.zero);
                activityType.GetMethod("SetDestination").Invoke(
                    firstActivity,
                    new object[] { new Vector3(-2f, 0f, 0f) });
                activityType.GetMethod("Tick").Invoke(
                    firstActivity,
                    new object[] { 0.001f });
                Vector3 coincidentEscape = (Vector3)firstStack.GetType()
                    .GetProperty("TargetPosition").GetValue(firstStack);
                Assert.That(
                    coincidentEscape.x,
                    Is.LessThan(0f),
                    "Coincident NPCs must be able to separate even when the first frame step is very small.");
            }
            finally
            {
                managerInstance.SetValue(null, previousManager);
                Object.DestroyImmediate(manager.gameObject);
                DestroyTestCard(first);
                DestroyTestCard(second);
            }
        }

        [Test]
        public void BackpackBoard_FlipsTheTopSurfaceTextureVertically()
        {
            System.Type boardType = FindType("CryingSnow.StackCraft.BackpackBoardView");
            var boardObject = new GameObject("BackpackTextureOrientationTest");
            var texture = new Texture2D(2, 2);

            try
            {
                Component board = boardObject.AddComponent(boardType);
                boardType.GetMethod("Initialize")
                    .Invoke(board, new object[] { null, texture });
                Material material = boardObject
                    .GetComponentInChildren<MeshRenderer>(true)
                    .sharedMaterial;

                Assert.That(material.mainTextureScale.x, Is.EqualTo(1f));
                Assert.That(material.mainTextureScale.y, Is.EqualTo(-1f),
                    "Unity cube top-face UVs invert this backpack artwork vertically.");
                Assert.That(material.mainTextureOffset.x, Is.EqualTo(0f));
                Assert.That(material.mainTextureOffset.y, Is.EqualTo(1f),
                    "A +1 V offset must accompany the negative V scale.");
            }
            finally
            {
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(boardObject);
            }
        }

        [Test]
        public void BackpackOpenButton_TogglesAnOpenedBackpackClosed()
        {
            System.Type viewType = FindType("CryingSnow.StackCraft.BackpackView");
            var viewObject = new GameObject("BackpackViewToggleTest");
            var panelObject = new GameObject(
                "BackpackPanel",
                typeof(RectTransform));
            panelObject.transform.SetParent(viewObject.transform, false);

            try
            {
                Component view = viewObject.AddComponent(viewType);
                viewType.GetField(
                        "tablePanel",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(view, panelObject.GetComponent<RectTransform>());

                MethodInfo toggle = viewType.GetMethod(
                    "Toggle",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(toggle, Is.Not.Null,
                    "The same backpack button must be able to open and close the board.");

                viewType.GetMethod("Open").Invoke(view, null);
                Assert.That(
                    (bool)viewType.GetProperty("IsOpen").GetValue(view),
                    Is.True);
                toggle.Invoke(view, null);
                Assert.That(
                    (bool)viewType.GetProperty("IsOpen").GetValue(view),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(viewObject);
            }
        }

        [Test]
        public void BackpackBoard_KeepsOnlyTheCloseControlOnTheOpenedSurface()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            GameObject instance = Object.Instantiate(prefab);

            try
            {
                Transform root = FindDescendant(instance, "BackpackRoot");
                Component view = root.GetComponents<MonoBehaviour>()
                    .Single(component => component.GetType().FullName ==
                        "CryingSnow.StackCraft.BackpackView");
                System.Type viewType = view.GetType();
                RectTransform panel = viewType.GetField(
                        "tablePanel",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(view) as RectTransform;
                Button close = viewType.GetField(
                        "closeButton",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(view) as Button;

                viewType.GetMethod(
                        "ConfigureLegacyPanelFor3D",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(view, null);
                viewType.GetMethod("Open").Invoke(view, null);

                Assert.That(close, Is.Not.Null);
                Assert.That(close.gameObject.activeSelf, Is.True);
                Assert.That(
                    close.transform.parent.GetInstanceID(),
                    Is.EqualTo(root.GetInstanceID()),
                    "The close button must follow the 3D board instead of the old side panel.");
                Assert.That(
                    panel.GetComponentsInChildren<Graphic>(true)
                        .All(graphic => !graphic.enabled),
                    Is.True,
                    "The legacy title, capacity, background and slot graphics must stay hidden.");
                Assert.That(
                    panel.GetComponentsInChildren<Selectable>(true)
                        .All(selectable => !selectable.gameObject.activeSelf),
                    Is.True,
                    "No legacy panel controls should remain beside the backpack.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BackpackBoard_ConvertsTheDraggedCardPositionWithoutLosingGrabOffset()
        {
            System.Type boardType = FindType("CryingSnow.StackCraft.BackpackBoardView");
            var boardObject = new GameObject("BackpackDropPositionTest");

            try
            {
                Component board = boardObject.AddComponent(boardType);
                board.transform.position = new Vector3(10f, 0f, -4f);
                MethodInfo convert = boardType.GetMethod(
                    "TryGetLocalTablePosition",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(Vector3),
                        FindType("CryingSnow.StackCraft.CardStack"),
                        typeof(Vector2).MakeByRefType()
                    },
                    null);
                Assert.That(convert, Is.Not.Null,
                    "Storage must persist the dragged card position, not snap its centre to the pointer ray.");

                object[] arguments =
                {
                    new Vector3(11.2f, 0.4f, -4.7f),
                    null,
                    Vector2.zero
                };
                Assert.That(convert.Invoke(board, arguments), Is.True);
                Vector2 local = (Vector2)arguments[2];
                Assert.That(local.x, Is.EqualTo(1.2f).Within(0.001f));
                Assert.That(local.y, Is.EqualTo(-0.7f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(boardObject);
            }
        }

        [Test]
        public void BackpackBoard_MatchesTheWorldTableStackAttachRange()
        {
            System.Type boardType = FindType("CryingSnow.StackCraft.BackpackBoardView");
            System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            MethodInfo overlaps = boardType.GetMethod(
                "IsWithinNormalAttachRange",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(Vector3), stackType, typeof(float) },
                null);
            Assert.That(overlaps, Is.Not.Null,
                "Backpack stacking should query real target colliders just like Physics.OverlapSphere on the world board.");

            Component target = CreateUninitializedCard(
                null,
                "Backpack Attach Range Target");
            try
            {
                BoxCollider collider = target.GetComponent<BoxCollider>();
                collider.size = new Vector3(0.8f, 0.1f, 1f);
                SetTestCardStackPosition(target, Vector3.zero);
                object targetStack =
                    target.GetType().GetProperty("Stack").GetValue(target);
                Physics.SyncTransforms();

                bool nearColliderEdge = (bool)overlaps.Invoke(
                    null,
                    new[] { (object)new Vector3(0.49f, 0f, 0f), targetStack, 0.1f });
                Assert.That(nearColliderEdge, Is.True,
                    "A point within AttachRadius of the real collider should stack.");

                bool visualMarginOnly = (bool)overlaps.Invoke(
                    null,
                    new[] { (object)new Vector3(0.55f, 0f, 0f), targetStack, 0.1f });
                Assert.That(visualMarginOnly, Is.False,
                    "Visual card margin must not enlarge backpack attachment.");

                bool outsideVertically = (bool)overlaps.Invoke(
                    null,
                    new[] { (object)new Vector3(0f, 0.2f, 0f), targetStack, 0.1f });
                Assert.That(outsideVertically, Is.False,
                    "The backpack check must preserve the world OverlapSphere's three-dimensional distance.");
            }
            finally
            {
                DestroyTestCard(target);
            }
        }

        [Test]
        public void BackpackBoard_PushesApartCardsThatCannotStack()
        {
            System.Type boardType =
                FindType("CryingSnow.StackCraft.BackpackBoardView");
            System.Type proxyType =
                FindType("CryingSnow.StackCraft.BackpackCardProxy");
            GameObject boardObject = new("Backpack Collision Board");
            Component firstCard = CreateUninitializedCard(
                null,
                "Backpack Nonstackable First");
            Component secondCard = CreateUninitializedCard(
                null,
                "Backpack Nonstackable Second");
            Object firstSettings = (Object)firstCard.GetType()
                .GetProperty("Settings").GetValue(firstCard);
            Object secondSettings = (Object)secondCard.GetType()
                .GetProperty("Settings").GetValue(secondCard);

            try
            {
                Component board = boardObject.AddComponent(boardType);
                firstCard.GetType().GetProperty("Size")
                    .SetValue(firstCard, Vector2.one);
                secondCard.GetType().GetProperty("Size")
                    .SetValue(secondCard, Vector2.one);
                SetTestCardStackPosition(firstCard, Vector3.zero);
                SetTestCardStackPosition(secondCard, Vector3.zero);

                Component firstProxy =
                    firstCard.gameObject.AddComponent(proxyType);
                Component secondProxy =
                    secondCard.gameObject.AddComponent(proxyType);
                proxyType.GetMethod("Bind").Invoke(
                    firstProxy,
                    new object[] { null, board, firstCard, "first", 0 });
                proxyType.GetMethod("Bind").Invoke(
                    secondProxy,
                    new object[] { null, board, secondCard, "second", 1 });

                var proxies = (IDictionary)boardType.GetField(
                        "proxies",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(board);
                proxies.Add("first", firstProxy);
                proxies.Add("second", secondProxy);

                Vector3 dropPosition = boardObject.transform.TransformPoint(
                    new Vector3(0f, 0.5f, 0f));
                boardType.GetMethod(
                        "PlaceOnTable",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(board, new object[] { firstProxy, dropPosition });

                object firstStack = firstCard.GetType()
                    .GetProperty("Stack").GetValue(firstCard);
                object secondStack = secondCard.GetType()
                    .GetProperty("Stack").GetValue(secondCard);
                Vector3 firstPosition = (Vector3)firstStack.GetType()
                    .GetProperty("TargetPosition").GetValue(firstStack);
                Vector3 secondPosition = (Vector3)secondStack.GetType()
                    .GetProperty("TargetPosition").GetValue(secondStack);

                Assert.That(
                    Vector2.Distance(
                        new Vector2(firstPosition.x, firstPosition.z),
                        new Vector2(secondPosition.x, secondPosition.z)),
                    Is.GreaterThanOrEqualTo(0.99f),
                    "Cards rejected by CanStack must separate like ordinary world-table stacks.");
            }
            finally
            {
                Object.DestroyImmediate(boardObject);
                if (firstCard != null)
                    Object.DestroyImmediate(firstCard.gameObject);
                if (secondCard != null)
                    Object.DestroyImmediate(secondCard.gameObject);
                if (firstSettings != null)
                    Object.DestroyImmediate(firstSettings);
                if (secondSettings != null)
                    Object.DestroyImmediate(secondSettings);
            }
        }

        [Test]
        public void BackpackBoard_ClampsAnOffsetStackByItsFullFootprint()
        {
            System.Type boardType = FindType("CryingSnow.StackCraft.BackpackBoardView");
            MethodInfo clamp = boardType.GetMethod(
                "ClampFootprintAnchor",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(clamp, Is.Not.Null);

            float anchor = (float)clamp.Invoke(
                null,
                new object[]
                {
                    -2f,
                    2.1f,
                    -1.81f,
                    0.55f
                });
            Assert.That(anchor, Is.EqualTo(-0.29f).Within(0.001f));
            Assert.That(anchor - 1.81f, Is.EqualTo(-2.1f).Within(0.001f),
                "A multi-card stack must keep its expanded lower edge on the backpack.");

            float oversized = (float)clamp.Invoke(
                null,
                new object[]
                {
                    -3f,
                    2.1f,
                    -4.5f,
                    0.5f
                });
            Assert.That(oversized, Is.EqualTo(2f).Within(0.001f),
                "An oversized stack should be centred so overflow is shared by both edges.");
        }

        [Test]
        public void BackpackView_UsesTheBrownOutlinedBackpackTexture()
        {
            System.Type viewType = FindType("CryingSnow.StackCraft.BackpackView");
            FieldInfo resourcePath = viewType.GetField(
                "BackgroundResourcePath",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resourcePath, Is.Not.Null);
            Assert.That(
                resourcePath.GetRawConstantValue(),
                Is.EqualTo("UI/BackpackTableBackground_Brown"));
            Assert.That(
                Resources.Load<Texture2D>("UI/BackpackTableBackground_Brown"),
                Is.Not.Null);
        }

        [Test]
        public void BackpackCards_DoNotRegisterSplitStacksWithWorldManager()
        {
            System.Type policyType =
                FindType("CryingSnow.StackCraft.ICardStackRegistrationPolicy");
            System.Type proxyType = FindType("CryingSnow.StackCraft.BackpackCardProxy");

            Assert.That(policyType, Is.Not.Null);
            Assert.That(policyType.IsAssignableFrom(proxyType), Is.True,
                "背包内部拆分堆叠时不能把新堆注册到世界地图");
            Assert.That(
                FindType("CryingSnow.StackCraft.CardController").GetMethod(
                    "AllowsNativeCardInteraction",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "背包代理不能执行宝箱开启等原生世界点击行为");
            Assert.That(
                FindType("CryingSnow.StackCraft.CardInstance").GetMethod(
                    "IsWorldStackCandidate",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Not.Null,
                "世界卡搜索附近堆时必须排除未注册的背包视觉卡");
        }

        [Test]
        public void BackpackService_TransfersAFreeTableStackAtomically()
        {
            System.Type serviceType = FindType("CryingSnow.StackCraft.BackpackService");
            System.Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            MethodInfo transferStack = serviceType.GetMethod(
                "TryTakeExistingStack",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    backpackType,
                    typeof(System.Collections.Generic.IReadOnlyCollection<string>),
                    typeof(System.Func<bool>)
                },
                null);
            Assert.That(transferStack, Is.Not.Null,
                "从背包拖出一个堆叠时，必须整体转移对应数据，不能只删除被点中的一张");

            object backpack = System.Activator.CreateInstance(backpackType);
            var ids = new List<string>();
            for (int index = 0; index < 2; index++)
            {
                object cardData = System.Activator.CreateInstance(cardDataType);
                cardDataType.GetField("Id").SetValue(cardData, $"egg-{index}");
                object[] addArguments = { cardData, null };
                backpackType.GetMethod("TryAdd").Invoke(backpack, addArguments);
                object entry = addArguments[1];
                ids.Add((string)entry.GetType().GetField("InstanceId").GetValue(entry));
            }

            int countDuringRejectedTransfer = -1;
            Assert.That(
                transferStack.Invoke(
                    null,
                    new object[]
                    {
                        backpack,
                        ids,
                        new System.Func<bool>(() =>
                        {
                            countDuringRejectedTransfer =
                                (int)backpackType.GetProperty("Count").GetValue(backpack);
                            return false;
                        })
                    }),
                Is.False);
            Assert.That(countDuringRejectedTransfer, Is.Zero,
                "转移回调开始前数据必须先完整移出背包，避免世界和背包同时持有同一堆卡");
            Assert.That(backpackType.GetProperty("Count").GetValue(backpack), Is.EqualTo(2),
                "转移失败后必须把整堆数据和布局原样回滚");

            Assert.That(
                transferStack.Invoke(
                    null,
                    new object[] { backpack, ids, new System.Func<bool>(() => true) }),
                Is.True);
            Assert.That(backpackType.GetProperty("Count").GetValue(backpack), Is.Zero);
        }

        [Test]
        public void BackpackService_RollsBackWorldSideEffectsWhenStackTransferFails()
        {
            System.Type serviceType = FindType("CryingSnow.StackCraft.BackpackService");
            System.Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            MethodInfo transferStack = serviceType.GetMethod(
                "TryTakeExistingStack",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    backpackType,
                    typeof(System.Collections.Generic.IReadOnlyCollection<string>),
                    typeof(System.Func<bool>),
                    typeof(System.Action)
                },
                null);
            Assert.That(transferStack, Is.Not.Null,
                "整堆转移需要显式世界侧回滚，避免异常后同时存在于背包和世界");

            object backpack = System.Activator.CreateInstance(backpackType);
            object cardData = System.Activator.CreateInstance(cardDataType);
            cardDataType.GetField("Id").SetValue(cardData, "egg");
            object[] addArguments = { cardData, null };
            backpackType.GetMethod("TryAdd").Invoke(backpack, addArguments);
            object entry = addArguments[1];
            var ids = new List<string>
            {
                (string)entry.GetType().GetField("InstanceId").GetValue(entry)
            };
            int worldSideEffects = 0;

            Assert.That(
                transferStack.Invoke(
                    null,
                    new object[]
                    {
                        backpack,
                        ids,
                        new System.Func<bool>(() =>
                        {
                            worldSideEffects++;
                            return false;
                        }),
                        new System.Action(() => worldSideEffects--)
                    }),
                Is.False);
            Assert.That(worldSideEffects, Is.Zero);
            Assert.That(backpackType.GetProperty("Count").GetValue(backpack), Is.EqualTo(1));
        }

        [Test]
        public void BackpackService_StoresWorldStackAtDroppedFreeTablePosition()
        {
            System.Type serviceType = FindType("CryingSnow.StackCraft.BackpackService");
            System.Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            System.Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
            MethodInfo storeAtPosition = serviceType.GetMethod(
                "TryStoreAtTablePosition",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { cardType, backpackType, typeof(Vector2) },
                null);
            Assert.That(storeAtPosition, Is.Not.Null,
                "地图卡拖入背包后应停在鼠标落点，不能重新跳到默认散点");

            Object eggDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Egg.asset");
            Component egg = CreateUninitializedCard(eggDefinition, "Free Table Egg");
            object backpack = System.Activator.CreateInstance(backpackType);
            try
            {
                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode!"));
                Assert.That(
                    storeAtPosition.Invoke(
                        null,
                        new object[] { egg, backpack, new Vector2(1.15f, -0.64f) }),
                    Is.True);
                object entry = ((IEnumerable)backpackType.GetField("Entries")
                        .GetValue(backpack))
                    .Cast<object>()
                    .Single();
                Assert.That(entry.GetType().GetField("HasTablePosition").GetValue(entry), Is.True);
                Assert.That(entry.GetType().GetField("TablePositionX").GetValue(entry), Is.EqualTo(1.15f));
                Assert.That(entry.GetType().GetField("TablePositionZ").GetValue(entry), Is.EqualTo(-0.64f));
            }
            finally
            {
                DestroyTestCard(egg);
            }
        }

        [Test]
        public void BackpackView_UsesDedicatedIllustratedTableBackground()
        {
            System.Type viewType = FindType("CryingSnow.StackCraft.BackpackView");
            FieldInfo resourcePath = viewType.GetField(
                "BackgroundResourcePath",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resourcePath, Is.Not.Null,
                "三维背包桌面必须使用独立的浅色简笔画背景，不能继续读取旧背包 UI 图片");
            Assert.That(
                resourcePath.GetRawConstantValue(),
                Is.EqualTo("UI/BackpackTableBackground_Brown"));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/StackCraft/Resources/UI/BackpackTableBackground.png"),
                Is.Not.Null);
        }

        [Test]
        public void CardManager_ProvidesUnmanagedRestoreForBackpackVisualCards()
        {
            System.Type cardManagerType = FindType("CryingSnow.StackCraft.CardManager");
            System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");

            Assert.That(
                cardManagerType.GetMethod(
                    "RestoreUnmanagedCardFromData",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { cardDataType, typeof(Vector3) },
                    null),
                Is.Not.Null,
                "背包桌面的视觉卡从创建开始就不能注册到世界卡牌堆，否则生成时会推动地图上的卡牌");
        }

        [Test]
        public void BackpackBoard_VisualCardsStayOutOfWorldSaveStacks()
        {
            EditorSceneManager.OpenScene(
                "Assets/StackCraft/Scenes/Location.unity",
                OpenSceneMode.Single);
            System.Type gameDirectorType = FindType("CryingSnow.StackCraft.GameDirector");
            System.Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            System.Type gameplayPrefsType = FindType("CryingSnow.StackCraft.GameplayPrefs");
            System.Type boardType = FindType("CryingSnow.StackCraft.BackpackBoardView");
            System.Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");

            MonoBehaviour gameDirector = (MonoBehaviour)new GameObject(
                    "3D Backpack Test GameDirector")
                .AddComponent(gameDirectorType);
            gameDirectorType.GetProperty(
                    "Instance",
                    BindingFlags.Public | BindingFlags.Static)
                .SetValue(null, gameDirector);
            object gameData = System.Activator.CreateInstance(gameDataType);
            gameDataType.GetField("GameplayPrefs").SetValue(
                gameData,
                System.Activator.CreateInstance(gameplayPrefsType));
            gameDirectorType.GetProperty("GameData").SetValue(gameDirector, gameData);

            MonoBehaviour cardManager = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.CardManager");
            System.Type cardManagerType = cardManager.GetType();
            cardManagerType.GetProperty(
                    "Instance",
                    BindingFlags.Public | BindingFlags.Static)
                .SetValue(null, cardManager);
            cardManagerType.GetMethod(
                    "InitializePrefabLookup",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cardManager, null);
            cardManagerType.GetMethod(
                    "BuildDefinitionDatabase",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cardManager, null);

            GameObject boardObject = new GameObject("3D Backpack Board Test");
            Component board = boardObject.AddComponent(boardType);
            try
            {
                boardType.GetMethod("Initialize").Invoke(board, new object[] { null, null });
                object backpack = System.Activator.CreateInstance(backpackType);
                object eggData = System.Activator.CreateInstance(cardDataType);
                Object eggDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Egg.asset");
                cardDataType.GetField("Id").SetValue(
                    eggData,
                    eggDefinition.GetType().GetProperty("Id").GetValue(eggDefinition));
                backpackType.GetMethod("TryAdd")
                    .Invoke(backpack, new object[] { eggData, null });

                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex(
                        "Instantiating material due to calling renderer.material during edit mode"));
                boardType.GetMethod("Rebuild").Invoke(board, new[] { backpack });

                MonoBehaviour proxy = boardObject.GetComponentsInChildren<MonoBehaviour>(true)
                    .Single(component => component.GetType().FullName ==
                        "CryingSnow.StackCraft.BackpackCardProxy");
                Component visualCard = proxy.GetType().GetProperty("Card").GetValue(proxy)
                    as Component;
                Assert.That(visualCard, Is.Not.Null,
                    "背包内容需要直接显示为原生 CardInstance");

                IEnumerable worldCards =
                    (IEnumerable)cardManagerType.GetProperty("AllCards").GetValue(cardManager);
                Assert.That(worldCards.Cast<object>().Contains(visualCard), Is.False,
                    "背包桌面上的视觉卡不能进入地点存档，否则会与 BackpackData 重复保存");
            }
            finally
            {
                Object.DestroyImmediate(boardObject);
                Object.DestroyImmediate(gameDirector.gameObject);
            }
        }

        [Test]
        public void BackpackView_UsesThreeDimensionalCardsWhenBoardIsAvailable()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            GameObject uiInstance = Object.Instantiate(prefab);
            try
            {
                Transform root = FindDescendant(uiInstance, "BackpackRoot");
                Component view = root.GetComponents<MonoBehaviour>().First(component =>
                    component.GetType().FullName == "CryingSnow.StackCraft.BackpackView");
                System.Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
                System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
                object backpack = System.Activator.CreateInstance(backpackType);
                Object appleDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset");
                Object coinDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/StackCraft/Resources/Cards/Currencies/Card_Coin.asset");
                Object eggDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Egg.asset");
                foreach (Object definition in new[]
                         {
                             appleDefinition,
                             coinDefinition,
                             eggDefinition
                         })
                {
                    object data = System.Activator.CreateInstance(cardDataType);
                    cardDataType.GetField("Id").SetValue(
                        data,
                        definition.GetType().GetProperty("Id").GetValue(definition));
                    object[] addArguments = { data, null };
                    backpackType.GetMethod("TryAdd").Invoke(backpack, addArguments);
                }
                backpackType.GetField("SlotCapacity").SetValue(backpack, 24);

                MethodInfo rebuild = view.GetType().GetMethod("Rebuild");
                Assert.That(rebuild, Is.Not.Null);
                rebuild.Invoke(view, new[] { backpack });

                Component board = view.GetType().GetProperty("Board3D").GetValue(view)
                    as Component;
                if (board != null)
                {
                    Assert.That(
                        root.GetComponentsInChildren<MonoBehaviour>(true).Count(component =>
                            component.GetType().FullName ==
                            "CryingSnow.StackCraft.BackpackItemView"),
                        Is.Zero,
                        "三维桌面可用时不应再生成二维背包卡");
                    Assert.That(
                        FindDescendant(root.gameObject, "BackpackCapacityText")
                            .GetComponent<TMPro.TMP_Text>().text,
                        Does.Contain("3/24"));
                    return;
                }

                Assert.That(
                    root.GetComponentsInChildren<MonoBehaviour>(true).Count(component =>
                        component.GetType().FullName == "CryingSnow.StackCraft.BackpackItemView"),
                    Is.EqualTo(3), "已改名或移除定义的旧物品也需要显示为占位卡，不能成为幽灵物品");
                Assert.That(
                    FindDescendant(root.gameObject, "BackpackCapacityText")
                        .GetComponent<TMPro.TMP_Text>().text,
                    Does.Contain("3/24"));
                Assert.That(
                    root.GetComponentsInChildren<Transform>(true).Count(child =>
                        child.name.StartsWith("BackpackSlot") &&
                        child.name != "BackpackSlots"),
                    Is.EqualTo(24), "自动扩容后 UI 也必须生成新格子");
                RectTransform table = (RectTransform)FindDescendant(
                    root.gameObject,
                    "BackpackTablePanel");
                RectTransform slots = (RectTransform)FindDescendant(
                    root.gameObject,
                    "BackpackSlots");
                Assert.That(table.sizeDelta.y, Is.LessThanOrEqualTo(440f),
                    "无上限背包不能把桌面面板顶出屏幕");
                Assert.That(slots.sizeDelta.y, Is.GreaterThan(440f),
                    "额外格子应扩展滚动内容高度，而不是扩展面板高度");
            }
            finally
            {
                Object.DestroyImmediate(uiInstance);
            }
        }

        [Test]
        public void CardController_ExposesBackpackDropAsAStandardDropAction()
        {
            System.Type controllerType = FindType("CryingSnow.StackCraft.CardController");
            MethodInfo tryStore = controllerType.GetMethod(
                "TryStoreInBackpack",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(tryStore, Is.Not.Null,
                "世界卡牌拖到背包入口或背包桌面时，需要由标准放下流程优先收纳");
            Assert.That(tryStore.ReturnType, Is.EqualTo(typeof(bool)));
            Assert.That(tryStore.GetParameters(), Is.Empty);
        }

        [Test]
        public void WorldMapPartyStatusView_ShowsPartyWhileGenericInfoPanelIsSuppressed()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            GameObject uiInstance = Object.Instantiate(prefab);
            Component card = null;
            ScriptableObject definition = null;
            Component statusView = null;
            Component infoPanel = null;
            try
            {
                Transform panel = FindDescendant(uiInstance, "WorldMapPartyStatusPanel");
                Assert.That(panel, Is.Not.Null);
                statusView = panel.GetComponents<MonoBehaviour>().First(component =>
                    component.GetType().FullName ==
                    "CryingSnow.StackCraft.WorldMapPartyStatusView");
                infoPanel = FindDescendant(uiInstance, "InfoPanel")
                    .GetComponents<MonoBehaviour>()
                    .First(component => component.GetType().FullName ==
                        "CryingSnow.StackCraft.InfoPanel");
                statusView.GetType().GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(statusView, null);
                infoPanel.GetType().GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(infoPanel, null);

                MethodInfo suppress = infoPanel.GetType().GetMethod("SetWorldMapSuppressed");
                Assert.That(suppress, Is.Not.Null);
                suppress.Invoke(infoPanel, new object[] { true });

                System.Type definitionType = FindType("CryingSnow.StackCraft.CardDefinition");
                definition = ScriptableObject.CreateInstance(definitionType);
                definitionType.GetMethod("SetDisplayName")
                    .Invoke(definition, new object[] { "旅行小队" });
                var serializedDefinition = new SerializedObject(definition);
                serializedDefinition.FindProperty("maxHealth").intValue = 15;
                serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
                card = CreateUninitializedCard(definition, "Fixed Party Status Card");
                card.GetType().GetField(
                    "<Stats>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(card, definitionType.GetMethod("CreateCombatStats").Invoke(definition, null));
                card.GetType().GetField(
                    "<CurrentHealth>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(card, 12);

                MethodInfo showParty = statusView.GetType().GetMethod("ShowParty");
                Assert.That(showParty, Is.Not.Null);
                showParty.Invoke(statusView, new object[] { card, "河湾村", "驻扎中", 1 });

                Assert.That(
                    FindDescendant(uiInstance, "InfoPanel").GetComponent<CanvasGroup>().alpha,
                    Is.EqualTo(0f));
                Assert.That(panel.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
                Assert.That(
                    FindDescendant(panel.gameObject, "PartyName").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo("旅行小队"));
                Assert.That(
                    FindDescendant(panel.gameObject, "PartyHealthText").GetComponent<TMPro.TMP_Text>().text,
                    Does.Contain("12/15"));
                Assert.That(
                    FindDescendant(panel.gameObject, "PartyLocationText").GetComponent<TMPro.TMP_Text>().text,
                    Does.Contain("河湾村"));
                Assert.That(
                    FindDescendant(panel.gameObject, "PartyMembersText").GetComponent<TMPro.TMP_Text>().text,
                    Does.Contain("1"));
                Assert.That(
                    FindDescendant(panel.gameObject, "PartyStateText").GetComponent<TMPro.TMP_Text>().text,
                    Does.Contain("驻扎中"));

                card.GetType().GetField(
                    "<CurrentHealth>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(card, 7);
                MethodInfo lateUpdate = statusView.GetType().GetMethod(
                    "LateUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(lateUpdate, Is.Not.Null,
                    "固定状态栏需要持续反映小队的实时生命变化");
                lateUpdate.Invoke(statusView, null);
                Assert.That(
                    FindDescendant(panel.gameObject, "PartyHealthText").GetComponent<TMPro.TMP_Text>().text,
                    Does.Contain("7/15"));
            }
            finally
            {
                statusView?.GetType().GetMethod(
                    "OnDestroy",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(statusView, null);
                if (card != null)
                    DestroyTestCard(card);
                if (definition != null)
                    Object.DestroyImmediate(definition);
                Object.DestroyImmediate(uiInstance);
            }
        }

        [Test]
        public void WorldMapPartyStatusView_UsesTravelingLocationWhilePartyIsMoving()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.WorldMapBootstrap");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            GameObject uiInstance = Object.Instantiate(prefab);
            Component card = null;
            ScriptableObject definition = null;
            Component statusView = null;
            try
            {
                statusView = FindDescendant(uiInstance, "WorldMapPartyStatusPanel")
                    .GetComponents<MonoBehaviour>()
                    .First(component => component.GetType().FullName ==
                        "CryingSnow.StackCraft.WorldMapPartyStatusView");
                statusView.GetType().GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(statusView, null);

                System.Type definitionType = FindType("CryingSnow.StackCraft.CardDefinition");
                System.Type controllerType = FindType("CryingSnow.StackCraft.WorldMapPartyController");
                definition = ScriptableObject.CreateInstance(definitionType);
                definitionType.GetMethod("SetDisplayName")
                    .Invoke(definition, new object[] { "旅行小队" });
                card = CreateUninitializedCard(definition, "Traveling Status Party");
                Component controller = card.gameObject.AddComponent(controllerType);
                controllerType.GetField(
                    "partyCard",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, card);
                controllerType.GetField(
                    "<CurrentLocationIndex>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, 0);
                controllerType.GetField(
                    "<IsTraveling>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, true);
                bootstrap.GetType().GetField(
                    "partyController",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(bootstrap, controller);

                bootstrap.GetType().GetMethod(
                    "RefreshPartyInfo",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(bootstrap, new object[] { "前往 低语森林" });

                Assert.That(
                    FindDescendant(uiInstance, "PartyLocationText").GetComponent<TMPro.TMP_Text>().text,
                    Does.Contain("旅途中"),
                    "小队移动期间不能继续显示为驻扎在出发地点");
            }
            finally
            {
                statusView?.GetType().GetMethod(
                    "OnDestroy",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(statusView, null);
                if (card != null)
                    DestroyTestCard(card);
                if (definition != null)
                    Object.DestroyImmediate(definition);
                Object.DestroyImmediate(uiInstance);
            }
        }

        [Test]
        public void WorldMapBootstrap_SwapsGenericInfoForFixedPartyStatusDuringItsLifecycle()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.WorldMapBootstrap");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            GameObject uiInstance = Object.Instantiate(prefab);
            Component statusView = FindDescendant(uiInstance, "WorldMapPartyStatusPanel")
                .GetComponents<MonoBehaviour>()
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.WorldMapPartyStatusView");
            Component infoPanel = FindDescendant(uiInstance, "InfoPanel")
                .GetComponents<MonoBehaviour>()
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.InfoPanel");
            try
            {
                MethodInfo statusAwake = statusView.GetType().GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo infoAwake = infoPanel.GetType().GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(statusAwake, Is.Not.Null);
                Assert.That(infoAwake, Is.Not.Null);
                statusAwake.Invoke(statusView, null);
                infoAwake.Invoke(infoPanel, null);

                bootstrap.GetType().GetField(
                    "worldMapTexture",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(bootstrap, null);
                MethodInfo start = bootstrap.GetType().GetMethod(
                    "Start",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo destroy = bootstrap.GetType().GetMethod(
                    "OnDestroy",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(start, Is.Not.Null);
                Assert.That(destroy, Is.Not.Null);

                start.Invoke(bootstrap, null);
                CanvasGroup infoVisibility = FindDescendant(uiInstance, "InfoPanel")
                    .GetComponent<CanvasGroup>();
                Assert.That(
                    (bool)infoPanel.GetType().GetProperty("IsWorldMapSuppressed").GetValue(infoPanel),
                    Is.True);
                Assert.That(infoVisibility.alpha, Is.EqualTo(0f));
                Assert.That(infoVisibility.interactable, Is.False);
                Assert.That(infoVisibility.blocksRaycasts, Is.False);

                destroy.Invoke(bootstrap, null);
                Assert.That(
                    (bool)infoPanel.GetType().GetProperty("IsWorldMapSuppressed").GetValue(infoPanel),
                    Is.False);
                Assert.That(infoVisibility.alpha, Is.EqualTo(1f));
                Assert.That(infoVisibility.interactable, Is.True);
                Assert.That(infoVisibility.blocksRaycasts, Is.True);
                Assert.That(
                    FindDescendant(uiInstance, "WorldMapPartyStatusPanel")
                        .GetComponent<CanvasGroup>().alpha,
                    Is.EqualTo(0f));
            }
            finally
            {
                statusView.GetType().GetMethod(
                    "OnDestroy",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(statusView, null);
                Object.DestroyImmediate(uiInstance);
            }
        }

        [Test]
        public void WorldMapLocation_SelectionPublishesSidebarSelectionChanges()
        {
            System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
            EventInfo selectionChanged = locationType.GetEvent("SelectionChanged");
            Assert.That(selectionChanged, Is.Not.Null,
                "右侧固定栏需要订阅地点选择事件，而不是每帧查找场景对象");

            Component card = CreateUninitializedCard(null, "Sidebar Selection Location");
            Component location = card.gameObject.AddComponent(locationType);
            object lastSelection = new object();
            System.Action<Component> handler = selected => lastSelection = selected;
            System.Delegate runtimeHandler = System.Delegate.CreateDelegate(
                selectionChanged.EventHandlerType,
                handler.Target,
                handler.Method);
            selectionChanged.AddEventHandler(null, runtimeHandler);
            try
            {
                locationType.GetMethod(
                    "Initialize",
                    new[] { typeof(int), card.GetType() })
                    .Invoke(location, new object[] { 0, card });
                locationType.GetMethod("SetSelected", new[] { typeof(bool), typeof(bool) })
                    .Invoke(location, new object[] { true, true });
                Assert.That(lastSelection, Is.SameAs(location));

                locationType.GetMethod("SetSelected", new[] { typeof(bool), typeof(bool) })
                    .Invoke(location, new object[] { false, true });
                Assert.That(lastSelection, Is.Null);
            }
            finally
            {
                selectionChanged.RemoveEventHandler(null, runtimeHandler);
                DestroyTestCard(card);
            }
        }

        [Test]
        public void OriginalUiRoot_LocationSidebarStartsUnavailableWithoutASelection()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            GameObject uiInstance = Object.Instantiate(prefab);
            Component locationView = FindDescendant(uiInstance, "LocationView")
                .GetComponents<MonoBehaviour>()
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.WorldMapLocationView");
            try
            {
                locationView.GetType().GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(locationView, null);

                Assert.That(
                    FindDescendant(uiInstance, "LocationToggle").GetComponent<Toggle>().interactable,
                    Is.False,
                    "未选择地点时不能打开带占位数据的地点页");
                Assert.That(
                    FindDescendant(uiInstance, "EnterLocationButton").GetComponent<Button>().interactable,
                    Is.False,
                    "地点内部玩法接入前，进入地点按钮不能表现为可用按钮");
            }
            finally
            {
                locationView.GetType().GetMethod(
                    "OnDestroy",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(locationView, null);
                Object.DestroyImmediate(uiInstance);
            }
        }

        [Test]
        public void WorldMapLocation_ReplacingSelectionPublishesOnlyTheNewLocation()
        {
            System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
            EventInfo selectionChanged = locationType.GetEvent("SelectionChanged");
            Component firstCard = CreateUninitializedCard(null, "First Sidebar Location");
            Component secondCard = CreateUninitializedCard(null, "Second Sidebar Location");
            Component firstLocation = firstCard.gameObject.AddComponent(locationType);
            Component secondLocation = secondCard.gameObject.AddComponent(locationType);
            var publishedSelections = new List<Component>();
            System.Action<Component> handler = selected => publishedSelections.Add(selected);
            System.Delegate runtimeHandler = System.Delegate.CreateDelegate(
                selectionChanged.EventHandlerType,
                handler.Target,
                handler.Method);
            selectionChanged.AddEventHandler(null, runtimeHandler);
            try
            {
                locationType.GetMethod("Initialize")
                    .Invoke(firstLocation, new object[] { 0, firstCard });
                locationType.GetMethod("Initialize")
                    .Invoke(secondLocation, new object[] { 1, secondCard });
                locationType.GetMethod("SetSelected", new[] { typeof(bool), typeof(bool) })
                    .Invoke(firstLocation, new object[] { true, true });
                publishedSelections.Clear();

                locationType.GetMethod("NotifyCardClicked", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { secondCard });
                locationType.GetMethod("SetSelected", new[] { typeof(bool), typeof(bool) })
                    .Invoke(secondLocation, new object[] { true, true });

                Assert.That(publishedSelections, Is.EqualTo(new[] { secondLocation }),
                    "地点切换应是原子的，不能在 A 到 B 之间广播空选择");
            }
            finally
            {
                selectionChanged.RemoveEventHandler(null, runtimeHandler);
                DestroyTestCard(firstCard);
                DestroyTestCard(secondCard);
            }
        }

        [Test]
        public void OriginalUiRoot_SelectedLocationAutomaticallyOpensAndPopulatesLocationView()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            GameObject uiInstance = Object.Instantiate(prefab);
            Component card = null;
            ScriptableObject definition = null;
            try
            {
                Component locationView = FindDescendant(uiInstance, "LocationView")
                    .GetComponents<MonoBehaviour>()
                    .First(component => component.GetType().FullName ==
                        "CryingSnow.StackCraft.WorldMapLocationView");
                locationView.GetType().GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(locationView, null);

                System.Type cardDefinitionType = FindType("CryingSnow.StackCraft.CardDefinition");
                System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
                System.Type detailsType = FindType("CryingSnow.StackCraft.WorldMapLocationDetails");
                definition = ScriptableObject.CreateInstance(cardDefinitionType);
                cardDefinitionType.GetMethod("SetDisplayName")
                    .Invoke(definition, new object[] { "测试森林" });
                cardDefinitionType.GetMethod("SetDescription")
                    .Invoke(definition, new object[] { "用于验证右侧地点栏。" });
                card = CreateUninitializedCard(definition, "Sidebar Bound Location");
                Component location = card.gameObject.AddComponent(locationType);
                object details = System.Activator.CreateInstance(detailsType);
                detailsType.GetField("locationType").SetValue(details, "森林");
                detailsType.GetField("dangerLevel").SetValue(details, 2);
                detailsType.GetField("travelTime").SetValue(details, "1秒（临时）");
                detailsType.GetField("possibleResources").SetValue(
                    details,
                    new List<string> { "草药", "木材" });
                detailsType.GetField("explorationProgress").SetValue(details, 0.35f);
                detailsType.GetField("description").SetValue(details, "用于验证右侧地点栏。");
                locationType.GetMethod(
                    "InitializeWithDetails",
                    new[] { typeof(int), card.GetType(), detailsType })
                    .Invoke(location, new[] { (object)0, card, details });

                locationType.GetMethod("SetSelected", new[] { typeof(bool), typeof(bool) })
                    .Invoke(location, new object[] { true, true });

                Assert.That(
                    FindDescendant(uiInstance, "LocationToggle").GetComponent<Toggle>().isOn,
                    Is.True);
                Assert.That(
                    FindDescendant(uiInstance, "LocationTitle").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo("测试森林"));
                Assert.That(
                    FindDescendant(uiInstance, "LocationResources").GetComponent<TMPro.TMP_Text>().text,
                    Does.Contain("草药"));
                Assert.That(
                    FindDescendant(uiInstance, "EnterLocationButton").GetComponent<Button>().interactable,
                    Is.False,
                    "地点内部玩法尚未接入时不能提供没有反馈的假按钮");

                detailsType.GetField("possibleResources").SetValue(details, null);
                locationView.GetType().GetMethod("ShowLocation")
                    .Invoke(locationView, new[] { location });
                Assert.That(
                    FindDescendant(uiInstance, "LocationResources").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo("可能资源\n"),
                    "异常或旧数据中的空资源列表不能让地点栏崩溃");

                locationType.GetMethod("SetSelected", new[] { typeof(bool), typeof(bool) })
                    .Invoke(location, new object[] { false, true });
                Assert.That(
                    FindDescendant(uiInstance, "QuestsToggle").GetComponent<Toggle>().isOn,
                    Is.True,
                    "取消地点选择后应回到任务页");
            }
            finally
            {
                DestroyTestCard(card);
                if (definition != null)
                    Object.DestroyImmediate(definition);
                Component locationView = FindDescendant(uiInstance, "LocationView")
                    ?.GetComponents<MonoBehaviour>()
                    .FirstOrDefault(component => component.GetType().FullName ==
                        "CryingSnow.StackCraft.WorldMapLocationView");
                locationView?.GetType().GetMethod(
                    "OnDestroy",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(locationView, null);
                Object.DestroyImmediate(uiInstance);
            }
        }

        [Test]
        public void OriginalUiRoot_LocationActionShowsEnterAtCurrentLocationAndTravelsToAnother()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component =>
                    component.GetType().FullName == "CryingSnow.StackCraft.WorldMapBootstrap");
            Assert.That(bootstrap, Is.Not.Null);

            bootstrap.GetType().GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(bootstrap, null);

            var serialized = new SerializedObject(bootstrap);
            SerializedProperty locationSpawns = serialized.FindProperty("locationSpawns");
            SerializedProperty partyDefinition = serialized.FindProperty("partyDefinition");
            int originIndex = serialized.FindProperty("initialPartyLocationIndex").intValue;
            int destinationIndex = Enumerable.Range(0, locationSpawns.arraySize)
                .First(index => index != originIndex && (bool)bootstrap.GetType()
                    .GetMethod("AreLocationsConnected")
                    .Invoke(bootstrap, new object[] { originIndex, index }));
            MethodInfo configure = bootstrap.GetType().GetMethod("ConfigureSpawnedCard");

            GameObject uiInstance = null;
            Component party = null;
            var locations = new List<Component>();
            try
            {
                for (int index = 0; index < locationSpawns.arraySize; index++)
                {
                    SerializedProperty spawn = locationSpawns.GetArrayElementAtIndex(index);
                    Component locationCard = CreateUninitializedCard(
                        spawn.FindPropertyRelative("definition").objectReferenceValue,
                        $"Action Location {index}");
                    SetTestCardStackPosition(
                        locationCard,
                        spawn.FindPropertyRelative("position").vector3Value);
                    configure.Invoke(bootstrap, new object[] { locationCard });
                    locations.Add(locationCard);
                }

                party = CreateUninitializedCard(partyDefinition.objectReferenceValue, "Action Party");
                SetTestCardStackPosition(
                    party,
                    locationSpawns.GetArrayElementAtIndex(originIndex)
                        .FindPropertyRelative("position").vector3Value);
                configure.Invoke(bootstrap, new object[] { party });

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
                uiInstance = Object.Instantiate(prefab);
                Component locationView = FindDescendant(uiInstance, "LocationView")
                    .GetComponents<MonoBehaviour>()
                    .First(component => component.GetType().FullName ==
                        "CryingSnow.StackCraft.WorldMapLocationView");
                locationView.GetType().GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(locationView, null);

                Component origin = locations[originIndex].GetComponent("WorldMapLocation");
                locationView.GetType().GetMethod("ShowLocation")
                    .Invoke(locationView, new[] { origin });
                Button actionButton = FindDescendant(uiInstance, "EnterLocationButton")
                    .GetComponent<Button>();
                Assert.That(actionButton.interactable, Is.True);
                Assert.That(
                    actionButton.GetComponentInChildren<TMPro.TMP_Text>(true).text,
                    Is.EqualTo("进入地点"));

                Component destination = locations[destinationIndex].GetComponent("WorldMapLocation");
                locationView.GetType().GetMethod("ShowLocation")
                    .Invoke(locationView, new[] { destination });
                Assert.That(actionButton.interactable, Is.True);
                Assert.That(
                    actionButton.GetComponentInChildren<TMPro.TMP_Text>(true).text,
                    Is.EqualTo("旅行到这个地点"));

                actionButton.onClick.Invoke();

                Component controller = party.GetComponent("WorldMapPartyController");
                Assert.That(
                    controller.GetType().GetProperty("IsTraveling").GetValue(controller),
                    Is.True,
                    "点击地点栏的旅行按钮必须启动现有的小队堆叠旅行流程");
                Assert.That(
                    destination.GetType().GetProperty("IsTravelHighlighted").GetValue(destination),
                    Is.True);
                Assert.That(actionButton.interactable, Is.False);
                Assert.That(
                    actionButton.GetComponentInChildren<TMPro.TMP_Text>(true).text,
                    Is.EqualTo("旅行中…"));

                controller.GetType().GetMethod(
                    "TickTravel",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { 1f });

                Assert.That(
                    controller.GetType().GetProperty("CurrentLocationIndex").GetValue(controller),
                    Is.EqualTo(destinationIndex));
                Assert.That(actionButton.interactable, Is.False);
                Assert.That(
                    actionButton.GetComponentInChildren<TMPro.TMP_Text>(true).text,
                    Is.EqualTo("地点地图开发中"),
                    "第一阶段抵达河湾村以外的地点后，应保留旅行结果但不能进入未实现的局部地图");
            }
            finally
            {
                if (uiInstance != null)
                {
                    Component locationView = FindDescendant(uiInstance, "LocationView")
                        ?.GetComponents<MonoBehaviour>()
                        .FirstOrDefault(component => component.GetType().FullName ==
                            "CryingSnow.StackCraft.WorldMapLocationView");
                    locationView?.GetType().GetMethod(
                        "OnDestroy",
                        BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(locationView, null);
                    Object.DestroyImmediate(uiInstance);
                }

                DestroyTestCard(party);
                foreach (Component location in locations)
                    DestroyTestCard(location);
            }
        }

        [Test]
        public void GameData_RiverbendLocationUsesItsOwnPersistentSceneScope()
        {
            System.Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Assert.That(gameDataType, Is.Not.Null);

            FieldInfo activeLocation = gameDataType.GetField("ActiveLocationId");
            FieldInfo partyMembers = gameDataType.GetField("PartyMembers");
            Assert.That(activeLocation, Is.Not.Null,
                "通用地点场景必须保存当前地点 ID，不能只记住 Unity 场景名");
            Assert.That(partyMembers, Is.Not.Null,
                "小队进入局部地图前必须拥有可跨场景恢复的成员数据");

            object gameData = System.Activator.CreateInstance(gameDataType);
            gameDataType.GetField("CurrentScene").SetValue(gameData, "Location");
            activeLocation.SetValue(gameData, "riverbend");

            object[] arguments = { null };
            bool wasLoaded = (bool)gameDataType.GetMethod("TryGetScene")
                .Invoke(gameData, arguments);
            Assert.That(wasLoaded, Is.False);
            Assert.That(
                arguments[0].GetType().GetField("SceneName").GetValue(arguments[0]),
                Is.EqualTo("Location/riverbend"),
                "河湾村和未来的森林不能覆盖同一个 Location 存档槽");
        }

        [Test]
        public void MainScene_ConfiguresRiverbendAsTheFirstEnterableLocation()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.WorldMapBootstrap");
            var serialized = new SerializedObject(bootstrap);
            SerializedProperty firstDetails = serialized.FindProperty("locationDetails")
                .GetArrayElementAtIndex(0);
            SerializedProperty locationId = firstDetails.FindPropertyRelative("locationId");

            Assert.That(locationId, Is.Not.Null);
            Assert.That(locationId.stringValue, Is.EqualTo("riverbend"));
            Assert.That(
                bootstrap.GetType().GetMethod("TryEnterPartyLocation"),
                Is.Not.Null,
                "世界地图的进入按钮需要调用真实地点切换入口");
            Assert.That(
                serialized.FindProperty("legacyTravelerDefinition").objectReferenceValue.name,
                Is.EqualTo("Card_Villager"),
                "世界地图的小队卡进入河湾村后应展开为成员卡，而不是继续显示小队汇总卡");
        }

        [Test]
        public void MainScene_OnlyCompletedLocationsHaveImplementedLocalMaps()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.WorldMapBootstrap");
            SerializedProperty details = new SerializedObject(bootstrap)
                .FindProperty("locationDetails");

            for (int index = 0; index < details.arraySize; index++)
            {
                SerializedProperty entry = details.GetArrayElementAtIndex(index);
                string locationId =
                    entry.FindPropertyRelative("locationId").stringValue;
                bool expected = locationId == "riverbend" ||
                    locationId == "whispering-forest";
                SerializedProperty implemented = entry.FindPropertyRelative("localMapImplemented");
                Assert.That(implemented, Is.Not.Null,
                    "地点配置必须明确标记是否已有局部地图，避免未完成地点进入空场景");
                Assert.That(implemented.boolValue, Is.EqualTo(expected),
                    "当前只应开放已经完成局部地图的河湾村和低语森林");
            }
        }

        [Test]
        public void WorldMapBootstrap_AppliesReturnedMemberStatsAfterSavedWorldStackRestoration()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.WorldMapBootstrap");
            bootstrap.GetType().GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(bootstrap, null);

            Component party = null;
            try
            {
                var serialized = new SerializedObject(bootstrap);
                SerializedProperty spawns = serialized.FindProperty("locationSpawns");
                int originIndex = serialized.FindProperty("initialPartyLocationIndex").intValue;
                party = CreateUninitializedCard(
                    serialized.FindProperty("partyDefinition").objectReferenceValue,
                    "Returned Party State");
                SetTestCardStackPosition(
                    party,
                    spawns.GetArrayElementAtIndex(originIndex)
                        .FindPropertyRelative("position").vector3Value);
                bootstrap.GetType().GetMethod("ConfigureSpawnedCard")
                    .Invoke(bootstrap, new object[] { party });

                System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
                object staleWorldData = System.Activator.CreateInstance(cardDataType);
                cardDataType.GetField("CurrentHealth").SetValue(staleWorldData, 15);
                cardDataType.GetField("CurrentNutrition").SetValue(staleWorldData, 4);
                cardDataType.GetField("UsesLeft").SetValue(staleWorldData, 1);
                party.GetType().GetMethod("RestoreSavedStats")
                    .Invoke(party, new[] { staleWorldData });

                object returnedMemberData = System.Activator.CreateInstance(cardDataType);
                cardDataType.GetField("CurrentHealth").SetValue(returnedMemberData, 7);
                cardDataType.GetField("CurrentNutrition").SetValue(returnedMemberData, 2);
                cardDataType.GetField("UsesLeft").SetValue(returnedMemberData, 3);
                System.Type listType = typeof(List<>).MakeGenericType(cardDataType);
                object returnedMembers = System.Activator.CreateInstance(listType);
                listType.GetMethod("Add").Invoke(returnedMembers, new[] { returnedMemberData });

                MethodInfo applyReturnedState = bootstrap.GetType().GetMethod(
                    "ApplyReturnedPartyState",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(applyReturnedState, Is.Not.Null,
                    "返回 Main 后必须在世界地图旧堆栈恢复完成之后再应用地点内的人物状态");
                applyReturnedState.Invoke(bootstrap, new[] { returnedMembers });

                Assert.That(
                    party.GetType().GetProperty("CurrentHealth").GetValue(party),
                    Is.EqualTo(7));
                Assert.That(
                    party.GetType().GetProperty("CurrentNutrition").GetValue(party),
                    Is.EqualTo(2));
                Assert.That(
                    party.GetType().GetProperty("UsesLeft").GetValue(party),
                    Is.EqualTo(3));
            }
            finally
            {
                DestroyTestCard(party);
                bootstrap.GetType().GetMethod(
                    "OnDestroy",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(bootstrap, null);
            }
        }

        [Test]
        public void LocationScene_IsAReusableRiverbendBoardWithReturnAndExpandedPartyConfiguration()
        {
            const string scenePath = "Assets/StackCraft/Scenes/Location.unity";
            Assert.That(File.Exists(scenePath), Is.True,
                "第一阶段需要独立的通用地点桌面场景");
            Assert.That(
                EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == scenePath),
                Is.True,
                "地点场景必须进入 Build Settings 才能由进入按钮加载");

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            MonoBehaviour controller = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.LocationSceneController");
            Assert.That(controller, Is.Not.Null);

            var serializedController = new SerializedObject(controller);
            SerializedProperty definitions = serializedController.FindProperty("locationDefinitions");
            Assert.That(definitions, Is.Not.Null);
            Object riverbend = null;
            for (int index = 0; index < definitions.arraySize; index++)
            {
                Object candidate = definitions.GetArrayElementAtIndex(index).objectReferenceValue;
                if (candidate != null &&
                    new SerializedObject(candidate).FindProperty("id").stringValue == "riverbend")
                {
                    riverbend = candidate;
                    break;
                }
            }
            Assert.That(riverbend, Is.Not.Null);
            var serializedDefinition = new SerializedObject(riverbend);
            Assert.That(serializedDefinition.FindProperty("id").stringValue, Is.EqualTo("riverbend"));
            Assert.That(serializedDefinition.FindProperty("displayName").stringValue, Is.EqualTo("河湾村"));
            Assert.That(serializedDefinition.FindProperty("expandedPartyMemberDefinition")
                .objectReferenceValue.name, Is.EqualTo("Card_Villager"));
            Button returnButton = serializedController.FindProperty("returnButton")
                .objectReferenceValue as Button;
            Assert.That(returnButton, Is.Not.Null);
            Assert.That(returnButton.interactable, Is.True,
                "进入河湾村后必须能点击返回世界地图，不能继承一个禁用按钮状态");

            MonoBehaviour cardManager = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.CardManager");
            Assert.That(
                new SerializedObject(cardManager).FindProperty("defaultSpawnCards").arraySize,
                Is.Zero,
                "通用地点场景不能生成 Island 的旧默认卡牌");
            Assert.That(
                Object.FindObjectsOfType<MonoBehaviour>(true).Any(component =>
                    component.GetType().FullName == "CryingSnow.StackCraft.WorldMapBootstrap"),
                Is.False);
        }

        [Test]
        public void RiverbendLocation_ProvidesThreeExistingEggCardsForBackpackTesting()
        {
            Object riverbend = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset");
            Object egg = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Egg.asset");

            Assert.That(riverbend, Is.Not.Null);
            Assert.That(egg, Is.Not.Null);
            var serializedEgg = new SerializedObject(egg);
            Assert.That(serializedEgg.FindProperty("category").enumValueIndex, Is.EqualTo(3));
            Assert.That(serializedEgg.FindProperty("isLocationStatic").boolValue, Is.False,
                "测试鸡蛋必须能被玩家放入背包");

            SerializedProperty spawns =
                new SerializedObject(riverbend).FindProperty("initialCardSpawns");
            var eggPositions = new List<Vector2>();
            for (int index = 0; index < spawns.arraySize; index++)
            {
                SerializedProperty spawn = spawns.GetArrayElementAtIndex(index);
                if (AssetDatabase.GetAssetPath(
                        spawn.FindPropertyRelative("definition").objectReferenceValue) !=
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Egg.asset")
                    continue;

                Vector3 position = spawn.FindPropertyRelative("position").vector3Value;
                eggPositions.Add(new Vector2(position.x, position.z));
            }

            Assert.That(eggPositions, Has.Count.EqualTo(3),
                "河湾村应提供三张现有鸡蛋卡用于测试背包存取");
            Assert.That(eggPositions.Distinct().Count(), Is.EqualTo(3),
                "三张鸡蛋卡需要分开放置，避免初始堆叠影响测试");
        }

        [Test]
        public void RiverbendLocation_ConfiguresThreeBuildingsAndFourNpcCards()
        {
            Object riverbend = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset");
            Assert.That(riverbend, Is.Not.Null);

            var serializedLocation = new SerializedObject(riverbend);
            SerializedProperty spawns = serializedLocation.FindProperty("initialCardSpawns");
            Assert.That(spawns, Is.Not.Null,
                "地点定义需要拥有可复用的初始卡牌配置，而不是把河湾村卡牌写死在场景里");
            Assert.That(spawns.arraySize, Is.EqualTo(10));
            var expectedCategories = new Dictionary<string, int>
            {
                ["市场"] = 6,
                ["铁匠铺"] = 6,
                ["旅馆"] = 6,
                ["村长"] = 2,
                ["铁匠"] = 2,
                ["杂货商"] = 2,
                ["药师"] = 2
            };
            var actualNames = new HashSet<string>();
            var positions = new HashSet<Vector2>();
            var artPaths = new HashSet<string>();

            for (int index = 0; index < spawns.arraySize; index++)
            {
                SerializedProperty spawn = spawns.GetArrayElementAtIndex(index);
                Object definition = spawn.FindPropertyRelative("definition").objectReferenceValue;
                Assert.That(definition, Is.Not.Null);
                if (AssetDatabase.GetAssetPath(definition) ==
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Egg.asset")
                    continue;

                var serializedCard = new SerializedObject(definition);
                string displayName = serializedCard.FindProperty("displayName").stringValue;
                Assert.That(expectedCategories.ContainsKey(displayName), Is.True,
                    $"河湾村出现了未计划的初始卡牌：{displayName}");
                Assert.That(
                    serializedCard.FindProperty("category").enumValueIndex,
                    Is.EqualTo(expectedCategories[displayName]));
                Object artTexture = serializedCard.FindProperty("artTexture").objectReferenceValue;
                Assert.That(artTexture, Is.Not.Null,
                    $"{displayName} 需要有可辨认的专用卡面图");
                string artPath = AssetDatabase.GetAssetPath(artTexture);
                Assert.That(
                    artPath,
                    Does.StartWith("Assets/CardColony/Art/CardArts/Riverbend/"),
                    $"{displayName} 不应继续使用通用占位美术");
                Assert.That(artPaths.Add(artPath), Is.True,
                    $"{displayName} 应使用独立卡面图");

                if (expectedCategories[displayName] == 2)
                {
                    Assert.That(serializedCard.FindProperty("faction").enumValueIndex, Is.Zero,
                        $"{displayName} 是村庄中立 NPC，不能被当作玩家小队成员保存");
                }

                SerializedProperty isLocationStatic =
                    serializedCard.FindProperty("isLocationStatic");
                Assert.That(isLocationStatic, Is.Not.Null);
                Assert.That(isLocationStatic.boolValue, Is.True,
                    $"{displayName} 现阶段不应参与玩家生存结算或占用卡牌容量");

                Assert.That(actualNames.Add(displayName), Is.True, $"重复配置了 {displayName}");
                Vector3 position = spawn.FindPropertyRelative("position").vector3Value;
                Assert.That(positions.Add(new Vector2(position.x, position.z)), Is.True,
                    $"{displayName} 与另一张初始卡重叠");
            }

            Assert.That(actualNames, Is.EquivalentTo(expectedCategories.Keys));
            Assert.That(artPaths, Has.Count.EqualTo(7));
            Assert.That(
                FindType("CryingSnow.StackCraft.LocationSceneController").GetMethod(
                    "SpawnInitialLocationCards"),
                Is.Not.Null,
                "首次进入河湾村时需要把地点定义中的建筑和 NPC 真正生成到桌面");
        }

        [Test]
        public void RiverbendLocation_ArtUsesCardShaderMaskConvention()
        {
            Object riverbend = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset");
            var serializedLocation = new SerializedObject(riverbend);
            SerializedProperty spawns = serializedLocation.FindProperty("initialCardSpawns");

            Assert.That(spawns.arraySize, Is.EqualTo(10));
            for (int index = 0; index < spawns.arraySize; index++)
            {
                Object definition = spawns.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("definition").objectReferenceValue;
                if (AssetDatabase.GetAssetPath(definition) ==
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Egg.asset")
                    continue;

                var serializedCard = new SerializedObject(definition);
                string displayName = serializedCard.FindProperty("displayName").stringValue;
                Texture2D art = serializedCard.FindProperty("artTexture").objectReferenceValue as Texture2D;
                string artPath = AssetDatabase.GetAssetPath(art);
                var importer = AssetImporter.GetAtPath(artPath) as TextureImporter;
                Assert.That(importer, Is.Not.Null, $"{displayName} 的卡图缺少纹理导入器");
                Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromGrayScale),
                    $"{displayName} 的黑底必须在导入时转换为透明遮罩，否则 Card Shader 会把卡面覆盖成黑色");
                var mask = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(ImageConversion.LoadImage(mask, File.ReadAllBytes(artPath)), Is.True);
                    Color32[] corners =
                    {
                        mask.GetPixel(0, 0),
                        mask.GetPixel(mask.width - 1, 0),
                        mask.GetPixel(0, mask.height - 1),
                        mask.GetPixel(mask.width - 1, mask.height - 1)
                    };
                    Assert.That(corners.All(pixel => pixel.r < 16 && pixel.g < 16 && pixel.b < 16),
                        Is.True,
                        $"{displayName} 的卡图必须使用黑底遮罩；白底会被 Card Shader 显示成深色卡面");
                    Assert.That(mask.GetPixels32().Any(pixel =>
                            pixel.r > 240 && pixel.g > 240 && pixel.b > 240),
                        Is.True,
                        $"{displayName} 的线稿需要保留白色高亮，才能由 Card Shader 转成深色图案");
                }
                finally
                {
                    Object.DestroyImmediate(mask);
                }
            }
        }

        [Test]
        public void RiverbendLocation_CardsUseWhiteBodyBaseTextures()
        {
            Object riverbend = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset");
            var serializedLocation = new SerializedObject(riverbend);
            SerializedProperty spawns = serializedLocation.FindProperty("initialCardSpawns");
            var baseTexturePaths = new HashSet<string>();
            Object firstDefinition = null;
            Texture2D firstBaseTexture = null;

            for (int index = 0; index < spawns.arraySize; index++)
            {
                Object definition = spawns.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("definition").objectReferenceValue;
                if (AssetDatabase.GetAssetPath(definition) ==
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Egg.asset")
                    continue;

                firstDefinition ??= definition;
                var serializedCard = new SerializedObject(definition);
                string displayName = serializedCard.FindProperty("displayName").stringValue;
                SerializedProperty baseTextureProperty =
                    serializedCard.FindProperty("baseTextureOverride");
                Assert.That(baseTextureProperty, Is.Not.Null,
                    "地点卡需要可选底板覆盖，不能修改全局人物或建筑卡材质");
                Texture2D baseTexture = baseTextureProperty.objectReferenceValue as Texture2D;
                Assert.That(baseTexture, Is.Not.Null, $"{displayName} 缺少浅色地点卡底板");
                firstBaseTexture ??= baseTexture;
                baseTexturePaths.Add(AssetDatabase.GetAssetPath(baseTexture));

                var readableTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(ImageConversion.LoadImage(
                        readableTexture,
                        File.ReadAllBytes(AssetDatabase.GetAssetPath(baseTexture))), Is.True);
                    Color bodyColor = readableTexture.GetPixel(
                        readableTexture.width / 2,
                        readableTexture.height / 2);
                    Assert.That(bodyColor.r, Is.GreaterThan(0.94f), $"{displayName} 卡面不是白色");
                    Assert.That(bodyColor.g, Is.GreaterThan(0.94f), $"{displayName} 卡面不是白色");
                    Assert.That(bodyColor.b, Is.GreaterThan(0.94f), $"{displayName} 卡面不是白色");
                }
                finally
                {
                    Object.DestroyImmediate(readableTexture);
                }
            }

            Assert.That(baseTexturePaths, Has.Count.EqualTo(2),
                "建筑和 NPC 应共享两套统一白底模板，只保留标题栏类别色差异");

            System.Type cardInstanceType = FindType("CryingSnow.StackCraft.CardInstance");
            MethodInfo applyVisualTextures = cardInstanceType.GetMethod(
                "ApplyVisualTextures",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(applyVisualTextures, Is.Not.Null,
                "CardInstance 必须把地点卡的专用底板应用到材质实例");

            Material sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/StackCraft/Materials/Cards/Character.mat");
            var material = new Material(sourceMaterial);
            try
            {
                applyVisualTextures.Invoke(null, new[] { material, firstDefinition });
                Assert.That(material.GetTexture("_BaseTex"), Is.SameAs(firstBaseTexture));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void RiverbendLocation_BuildingsAndNpcsCannotBeDraggedByPlayer()
        {
            Object riverbend = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset");
            var serializedLocation = new SerializedObject(riverbend);
            SerializedProperty spawns = serializedLocation.FindProperty("initialCardSpawns");
            Object marketDefinition = null;
            Object villageChiefDefinition = null;

            for (int index = 0; index < spawns.arraySize; index++)
            {
                Object definition = spawns.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("definition").objectReferenceValue;
                if (AssetDatabase.GetAssetPath(definition) ==
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Egg.asset")
                    continue;

                var serializedCard = new SerializedObject(definition);
                string displayName = serializedCard.FindProperty("displayName").stringValue;
                SerializedProperty playerDraggable = serializedCard.FindProperty("playerDraggable");

                Assert.That(playerDraggable, Is.Not.Null,
                    $"{displayName} 需要显式配置玩家能否拖动");
                Assert.That(playerDraggable.boolValue, Is.False,
                    $"{displayName} 是地点固定内容，不能被玩家随意拖动");

                if (displayName == "市场") marketDefinition = definition;
                if (displayName == "村长") villageChiefDefinition = definition;
            }

            Component market = CreateUninitializedCard(marketDefinition, "Fixed Market");
            Component villageChief = CreateUninitializedCard(villageChiefDefinition, "Fixed Village Chief");
            try
            {
                System.Type controllerType = FindType("CryingSnow.StackCraft.CardController");
                Component marketController = market.gameObject.AddComponent(controllerType);
                Component chiefController = villageChief.gameObject.AddComponent(controllerType);
                MethodInfo controllerAwake = controllerType.GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                controllerAwake.Invoke(marketController, null);
                controllerAwake.Invoke(chiefController, null);
                PropertyInfo canBeDragged = controllerType.GetProperty("CanBeDragged");

                Assert.That(canBeDragged.GetValue(marketController), Is.False);
                Assert.That(villageChief.GetType().GetProperty("Definition").GetValue(villageChief),
                    Is.SameAs(villageChiefDefinition), "村长测试卡没有保留定义");
                object chiefStack = villageChief.GetType().GetProperty("Stack").GetValue(villageChief);
                Assert.That(chiefStack, Is.Not.Null, "村长测试卡缺少卡堆");
                Assert.That(chiefStack.GetType().GetProperty("IsLocked").GetValue(chiefStack), Is.False,
                    "村长卡堆不应锁定");
                Assert.That(villageChiefDefinition.GetType().GetProperty("PlayerDraggable")
                    .GetValue(villageChiefDefinition), Is.False, "村长定义的运行时拖动开关应为关闭");
                Assert.That(canBeDragged.GetValue(chiefController), Is.False,
                    "NPC 即使卡堆未锁定，CardController 也不能允许玩家拖动");
            }
            finally
            {
                DestroyTestCard(market);
                DestroyTestCard(villageChief);
            }
        }

        [Test]
        public void LocationNpcActivity_MovesTowardCommandsWithoutLeavingItsHomeRadius()
        {
            System.Type activityType = FindType("CryingSnow.StackCraft.LocationNpcActivity");
            Assert.That(activityType, Is.Not.Null,
                "地点 NPC 需要独立的轻量活动状态机，作为巡逻、散步和日程行为的基础");

            Object villageChiefDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_VillageChief.asset");
            Component villageChief = CreateUninitializedCard(villageChiefDefinition, "Ambient Village Chief");
            try
            {
                Component activity = villageChief.gameObject.AddComponent(activityType);
                activityType.GetMethod("Configure").Invoke(
                    activity,
                    new object[] { villageChief, Vector3.zero, 1f, 0.5f, new Vector2(2f, 3f) });
                activityType.GetMethod("SetDestination").Invoke(
                    activity,
                    new object[] { new Vector3(5f, 0f, 0f) });

                Vector3 destination = (Vector3)activityType.GetProperty("Destination").GetValue(activity);
                Assert.That(Vector3.Distance(Vector3.zero, destination), Is.LessThanOrEqualTo(1.001f));
                Assert.That(activityType.GetProperty("State").GetValue(activity).ToString(), Is.EqualTo("Moving"));

                activityType.GetMethod("Tick").Invoke(activity, new object[] { 1f });
                Assert.That(villageChief.transform.position.x, Is.GreaterThan(0f));
                Assert.That(villageChief.transform.position.x, Is.LessThanOrEqualTo(0.501f));
            }
            finally
            {
                DestroyTestCard(villageChief);
            }
        }

        [Test]
        public void RiverbendNpcDefinitions_ExposeBasicDialogueContent()
        {
            Object riverbend = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset");
            var serializedLocation = new SerializedObject(riverbend);
            SerializedProperty spawns = serializedLocation.FindProperty("initialCardSpawns");
            int dialogueNpcCount = 0;

            for (int index = 0; index < spawns.arraySize; index++)
            {
                Object definition = spawns.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("definition").objectReferenceValue;
                var serializedCard = new SerializedObject(definition);
                if (serializedCard.FindProperty("category").enumValueIndex != 2)
                    continue;

                dialogueNpcCount++;
                SerializedProperty enabled = serializedCard.FindProperty("dialogueEnabled");
                SerializedProperty opening = serializedCard.FindProperty("dialogueOpeningText");
                SerializedProperty reply = serializedCard.FindProperty("dialogueReplyText");
                SerializedProperty response = serializedCard.FindProperty("dialogueResponseText");

                Assert.That(enabled, Is.Not.Null, "NPC 卡牌定义需要可扩展的对话开关");
                Assert.That(opening, Is.Not.Null, "NPC 卡牌定义需要开场对白");
                Assert.That(reply, Is.Not.Null, "NPC 卡牌定义需要基础回复选项");
                Assert.That(response, Is.Not.Null, "NPC 卡牌定义需要回复后的对白");
                Assert.That(enabled.boolValue, Is.True);
                Assert.That(opening.stringValue, Is.Not.Empty);
                Assert.That(reply.stringValue, Is.Not.Empty);
                Assert.That(response.stringValue, Is.Not.Empty);
            }

            Assert.That(dialogueNpcCount, Is.EqualTo(4));
        }

        [Test]
        public void DialoguePanelPrefab_ContainsReferenceStyleConversationControls()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/DialoguePanel.prefab");
            Assert.That(prefab, Is.Not.Null, "需要把对话框制作成可复用的场景 UI 预制体");

            System.Type viewType = FindType("CryingSnow.StackCraft.DialoguePanelView");
            Assert.That(viewType, Is.Not.Null);
            Assert.That(prefab.GetComponent(viewType), Is.Not.Null);
            Assert.That(FindDescendant(prefab, "Portrait"), Is.Not.Null);
            Assert.That(FindDescendant(prefab, "PortraitBackground"), Is.Not.Null,
                "对话头像需要像卡牌一样叠加卡底，不能只显示白色线稿原图");
            Assert.That(FindDescendant(prefab, "SpeakerName"), Is.Not.Null);
            Assert.That(FindDescendant(prefab, "DialogueText"), Is.Not.Null);
            Assert.That(FindDescendant(prefab, "ReplyButton"), Is.Not.Null);
            Assert.That(FindDescendant(prefab, "GoodbyeButton"), Is.Not.Null);

            RawImage portrait = FindDescendant(prefab, "Portrait").GetComponent<RawImage>();
            Assert.That(portrait.color.r, Is.LessThan(0.4f),
                "NPC 白色线稿应在对话头像中着色为卡牌使用的深色线稿");
            Assert.That(prefab.activeSelf, Is.False, "对话框平时必须隐藏，只在对话时显示");
        }

        [Test]
        public void DialogueManager_AcceptsPlayerAndNeutralDialogueNpcButNotBuildings()
        {
            System.Type managerType = FindType("CryingSnow.StackCraft.DialogueManager");
            Assert.That(managerType, Is.Not.Null, "地点场景需要独立的基础对话管理器");
            MethodInfo canStart = managerType.GetMethod(
                "CanStartDialogue",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(canStart, Is.Not.Null);

            Object playerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Object chiefDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_VillageChief.asset");
            Object marketDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Market.asset");
            Component player = CreateUninitializedCard(playerDefinition, "Dialogue Player");
            Component chief = CreateUninitializedCard(chiefDefinition, "Dialogue Chief");
            Component market = CreateUninitializedCard(marketDefinition, "Dialogue Market");
            try
            {
                Assert.That(canStart.Invoke(null, new object[] { player, chief }), Is.True);
                Assert.That(canStart.Invoke(null, new object[] { chief, player }), Is.True,
                    "无论拖动人物还是 NPC，配对识别都应一致");
                Assert.That(canStart.Invoke(null, new object[] { player, market }), Is.False,
                    "建筑卡不能误触发人物对话");
            }
            finally
            {
                DestroyTestCard(player);
                DestroyTestCard(chief);
                DestroyTestCard(market);
            }
        }

        [Test]
        public void DialogueManager_StartAndEndMovesCardsThroughInteractionRectWithoutCombat()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Location.unity", OpenSceneMode.Single);
            string[] singletonTypes =
            {
                "CryingSnow.StackCraft.Board",
                "CryingSnow.StackCraft.InputManager",
                "CryingSnow.StackCraft.CardManager",
                "CryingSnow.StackCraft.CombatManager",
                "CryingSnow.StackCraft.DialogueManager"
            };
            foreach (string typeName in singletonTypes)
            {
                MonoBehaviour component = Object.FindObjectsOfType<MonoBehaviour>(true)
                    .First(item => item.GetType().FullName == typeName);
                component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(component, null);
            }

            Object playerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Object chiefDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_VillageChief.asset");
            Component player = CreateUninitializedCard(playerDefinition, "Live Dialogue Player");
            Component chief = CreateUninitializedCard(chiefDefinition, "Live Dialogue Chief");
            Component hunter = null;
            Component interactionRect = null;
            try
            {
                player.GetType().GetProperty("Size").SetValue(player, Vector2.one);
                chief.GetType().GetProperty("Size").SetValue(chief, Vector2.one);
                SetTestCardStackPosition(player, new Vector3(-0.2f, 0f, 0f));
                SetTestCardStackPosition(chief, new Vector3(0.2f, 0f, 0f));

                System.Type cardManagerType = FindType("CryingSnow.StackCraft.CardManager");
                object cardManager = cardManagerType.GetProperty("Instance").GetValue(null);
                object playerStack = player.GetType().GetProperty("Stack").GetValue(player);
                object chiefStack = chief.GetType().GetProperty("Stack").GetValue(chief);
                cardManagerType.GetMethod("RegisterStack").Invoke(cardManager, new[] { playerStack });
                cardManagerType.GetMethod("RegisterStack").Invoke(cardManager, new[] { chiefStack });

                System.Type combatantType = FindType("CryingSnow.StackCraft.CardCombatant");
                Component playerCombatant = player.gameObject.AddComponent(combatantType);
                Component chiefCombatant = chief.gameObject.AddComponent(combatantType);
                combatantType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(playerCombatant, null);
                combatantType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(chiefCombatant, null);
                player.GetType().GetProperty("Combatant").SetValue(player, playerCombatant);
                chief.GetType().GetProperty("Combatant").SetValue(chief, chiefCombatant);

                System.Type activityType = FindType("CryingSnow.StackCraft.LocationNpcActivity");
                Component activity = chief.gameObject.AddComponent(activityType);
                activityType.GetMethod("Configure").Invoke(
                    activity,
                    new object[] { chief, chief.transform.position, 1f, 0.5f, new Vector2(2f, 3f) });

                System.Type managerType = FindType("CryingSnow.StackCraft.DialogueManager");
                object manager = managerType.GetProperty("Instance").GetValue(null);
                bool started = (bool)managerType.GetMethod("StartDialogue")
                    .Invoke(manager, new object[] { player, chief });
                Assert.That(started, Is.True);
                Assert.That(managerType.GetProperty("IsActive").GetValue(manager), Is.True);
                Assert.That(player.GetType().GetProperty("Stack").GetValue(player), Is.Null);
                Assert.That(chief.GetType().GetProperty("Stack").GetValue(chief), Is.Null);
                Assert.That(combatantType.GetProperty("IsInCombat").GetValue(playerCombatant), Is.False);
                Assert.That(combatantType.GetProperty("IsInCombat").GetValue(chiefCombatant), Is.False);
                Assert.That(activityType.GetProperty("IsInteractionPaused").GetValue(activity), Is.True);
                interactionRect = (Component)managerType.GetProperty("InteractionRect").GetValue(manager);
                Assert.That(interactionRect, Is.Not.Null);
                Image interactionVisual = interactionRect.GetComponentInChildren<Image>(true);
                Assert.That(interactionVisual.material, Is.Not.Null);
                Assert.That(interactionVisual.material.shader.name,
                    Is.EqualTo("Crying Snow/StackCraft/InteractionTint"),
                    "对话继续复用战斗框结构，但必须使用独立的绿色视觉，不得修改战斗红框");
                Assert.That(
                    managerType.GetProperty("HasActiveParticipantAnimation").GetValue(manager),
                    Is.True,
                    "进入对话后双方卡牌应启动 DOTween 悬浮动画");

                var liveCards = ((IEnumerable)cardManagerType.GetProperty("AllCards")
                        .GetValue(cardManager))
                    .Cast<object>()
                    .ToList();
                Assert.That(liveCards, Does.Contain(player));
                Assert.That(liveCards, Does.Contain(chief),
                    "对话双方脱离卡堆后仍必须属于当前场景，避免存档或日结遗漏人物");

                Object hunterDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/StackCraft/Resources/Cards/Mobs/Card_Goblin.asset");
                hunter = CreateUninitializedCard(hunterDefinition, "Dialogue Safety Hunter");
                Component hunterCombatant = hunter.gameObject.AddComponent(combatantType);
                combatantType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(hunterCombatant, null);
                hunter.GetType().GetProperty("Combatant").SetValue(hunter, hunterCombatant);
                System.Type aiType = FindType("CryingSnow.StackCraft.CardAI");
                Component hunterAi = hunter.gameObject.AddComponent(aiType);
                aiType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(hunterAi, null);
                object huntedTarget = aiType.GetMethod(
                        "FindClosestPlayerCard",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(hunterAi, new object[] { 100f });
                Assert.That(huntedTarget, Is.Null,
                    "攻击性 AI 不得选中临时脱离卡堆的对话人物，否则攻击阶段会访问空 Stack");

                GameObject panel = GameObject.Find("DialoguePanel");
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.activeSelf, Is.True);

                MethodInfo beforeSave = managerType.GetMethod(
                    "HandleBeforeSave",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(beforeSave, Is.Not.Null,
                    "保存前应主动结束临时对话，把双方恢复到可序列化的卡堆中");
                beforeSave.Invoke(manager, new object[] { null });
                Assert.That(managerType.GetProperty("IsActive").GetValue(manager), Is.False);
                Assert.That(
                    managerType.GetProperty("HasActiveParticipantAnimation").GetValue(manager),
                    Is.False,
                    "结束对话时必须清理悬浮动画，不能把 Tween 留在已恢复的卡牌上");
                Assert.That(player.GetType().GetProperty("Stack").GetValue(player), Is.Not.Null);
                Assert.That(chief.GetType().GetProperty("Stack").GetValue(chief), Is.Not.Null);
                Assert.That(activityType.GetProperty("IsInteractionPaused").GetValue(activity), Is.False);
            }
            finally
            {
                DestroyTestCard(player);
                DestroyTestCard(chief);
                DestroyTestCard(hunter);
                if (interactionRect != null)
                    Object.DestroyImmediate(interactionRect.gameObject);
            }
        }

        [Test]
        public void LocationNpcActivity_InteractionPauseStopsAndResumesMovement()
        {
            System.Type activityType = FindType("CryingSnow.StackCraft.LocationNpcActivity");
            MethodInfo setPaused = activityType.GetMethod("SetInteractionPaused");
            Assert.That(setPaused, Is.Not.Null,
                "NPC 进入对话框后需要暂停散步，离开后再恢复");

            Object chiefDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_VillageChief.asset");
            Component chief = CreateUninitializedCard(chiefDefinition, "Paused Dialogue Chief");
            try
            {
                Component activity = chief.gameObject.AddComponent(activityType);
                activityType.GetMethod("Configure").Invoke(
                    activity,
                    new object[] { chief, Vector3.zero, 1f, 0.5f, new Vector2(2f, 3f) });
                activityType.GetMethod("SetDestination").Invoke(
                    activity,
                    new object[] { new Vector3(1f, 0f, 0f) });

                setPaused.Invoke(activity, new object[] { true });
                activityType.GetMethod("Tick").Invoke(activity, new object[] { 1f });
                Assert.That(chief.transform.position, Is.EqualTo(Vector3.zero));

                setPaused.Invoke(activity, new object[] { false });
                activityType.GetMethod("Tick").Invoke(activity, new object[] { 1f });
                Assert.That(chief.transform.position.x, Is.GreaterThan(0f));
            }
            finally
            {
                DestroyTestCard(chief);
            }
        }

        [Test]
        public void InputManager_DialogueLockBlocksCardsButAllowsCameraInput()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            System.Type inputType = FindType("CryingSnow.StackCraft.InputManager");
            var inputObject = new GameObject("Dialogue Camera Input Test");
            Component input = inputObject.AddComponent(inputType);
            object dialogueLock = new object();
            object hardLock = new object();
            try
            {
                inputType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(input, null);
                MethodInfo addScopedLock = inputType.GetMethod(
                    "AddLock",
                    new[] { typeof(object), typeof(bool) });
                Assert.That(addScopedLock, Is.Not.Null,
                    "输入锁需要区分允许镜头的对话锁与完全禁止输入的硬锁");

                addScopedLock.Invoke(input, new[] { dialogueLock, (object)true });
                Assert.That(inputType.GetProperty("IsInputEnabled").GetValue(input), Is.False);
                Assert.That(inputType.GetProperty("IsCameraInputEnabled").GetValue(input), Is.True,
                    "对话期间卡牌输入仍应锁定，但地图拖动与缩放应可用");

                addScopedLock.Invoke(input, new[] { hardLock, (object)false });
                Assert.That(inputType.GetProperty("IsCameraInputEnabled").GetValue(input), Is.False,
                    "转场等硬锁存在时不能被对话的镜头权限绕过");

                inputType.GetMethod("RemoveLock").Invoke(input, new[] { hardLock });
                Assert.That(inputType.GetProperty("IsCameraInputEnabled").GetValue(input), Is.True);
                inputType.GetMethod("RemoveLock").Invoke(input, new[] { dialogueLock });
                Assert.That(inputType.GetProperty("IsInputEnabled").GetValue(input), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(inputObject);
            }
        }

        [Test]
        public void LocationNpcActivity_PausesInsteadOfMovingAnAttachedCardStack()
        {
            System.Type activityType = FindType("CryingSnow.StackCraft.LocationNpcActivity");
            Object villageChiefDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_VillageChief.asset");
            Object marketDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Market.asset");
            Component villageChief = CreateUninitializedCard(villageChiefDefinition, "Attached Village Chief");
            Component market = CreateUninitializedCard(marketDefinition, "Attached Market");
            try
            {
                Component activity = villageChief.gameObject.AddComponent(activityType);
                activityType.GetMethod("Configure").Invoke(
                    activity,
                    new object[] { villageChief, Vector3.zero, 1f, 0.5f, new Vector2(2f, 3f) });
                activityType.GetMethod("SetDestination").Invoke(
                    activity,
                    new object[] { new Vector3(1f, 0f, 0f) });

                object chiefStack = villageChief.GetType().GetProperty("Stack").GetValue(villageChief);
                object marketStack = market.GetType().GetProperty("Stack").GetValue(market);
                chiefStack.GetType().GetMethod("MergeWith").Invoke(chiefStack, new[] { marketStack });

                activityType.GetMethod("Tick").Invoke(activity, new object[] { 1f });
                Assert.That(villageChief.transform.position, Is.EqualTo(Vector3.zero),
                    "NPC 与其他卡堆叠时应暂停环境活动，不能带着整堆卡一起散步");
            }
            finally
            {
                DestroyTestCard(villageChief);
                DestroyTestCard(market);
            }
        }

        [Test]
        public void LocationSceneController_AddsAmbientAiOnlyToConfiguredNpcCards()
        {
            Object marketDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Market.asset");
            Object villageChiefDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_VillageChief.asset");
            Component market = CreateUninitializedCard(marketDefinition, "AI Excluded Market");
            Component villageChief = CreateUninitializedCard(villageChiefDefinition, "AI Enabled Village Chief");
            try
            {
                System.Type controllerType = FindType("CryingSnow.StackCraft.LocationSceneController");
                MethodInfo configure = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(method => method.Name == "ConfigureLocationCardBehaviours" &&
                        method.GetParameters().Length == 1);
                Assert.That(configure, Is.Not.Null);

                System.Type cardType = market.GetType();
                System.Array cards = System.Array.CreateInstance(cardType, 2);
                cards.SetValue(market, 0);
                cards.SetValue(villageChief, 1);
                configure.Invoke(null, new object[] { cards });

                System.Type activityType = FindType("CryingSnow.StackCraft.LocationNpcActivity");
                Assert.That(market.GetComponent(activityType), Is.Null);
                Assert.That(villageChief.GetComponent(activityType), Is.Not.Null);
                object marketStack = market.GetType().GetProperty("Stack").GetValue(market);
                Assert.That(marketStack.GetType().GetProperty("IsLocked").GetValue(marketStack), Is.False,
                    "建筑不能锁死整个卡堆，否则叠在建筑上的普通卡无法拆出");
                Assert.That(marketStack.GetType().GetProperty("IsAnchored").GetValue(marketStack), Is.True,
                    "固定建筑卡堆应作为物理解算锚点，而不是输入锁");
            }
            finally
            {
                DestroyTestCard(market);
                DestroyTestCard(villageChief);
            }
        }

        [Test]
        public void RiverbendLocation_PlayerCardsStackedOnBuildingsCanStillBeDraggedAway()
        {
            Object marketDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Market.asset");
            Object playerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Component market = CreateUninitializedCard(marketDefinition, "Stacked Market");
            Component player = CreateUninitializedCard(playerDefinition, "Player On Market");
            try
            {
                System.Type cardType = market.GetType();
                System.Array cards = System.Array.CreateInstance(cardType, 2);
                cards.SetValue(market, 0);
                cards.SetValue(player, 1);
                System.Type locationControllerType = FindType("CryingSnow.StackCraft.LocationSceneController");
                locationControllerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .First(method => method.Name == "ConfigureLocationCardBehaviours" &&
                        method.GetParameters().Length == 1)
                    .Invoke(null, new object[] { cards });

                object marketStack = market.GetType().GetProperty("Stack").GetValue(market);
                object playerStack = player.GetType().GetProperty("Stack").GetValue(player);
                marketStack.GetType().GetMethod("MergeWith").Invoke(marketStack, new[] { playerStack });

                System.Type controllerType = FindType("CryingSnow.StackCraft.CardController");
                Component controller = player.gameObject.AddComponent(controllerType);
                controllerType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);

                Assert.That(controllerType.GetProperty("CanBeDragged").GetValue(controller), Is.True,
                    "玩家卡叠到建筑上后仍需允许拖出，建筑固定不能通过锁死整堆实现");
            }
            finally
            {
                DestroyTestCard(market);
                DestroyTestCard(player);
            }
        }

        [Test]
        public void LocationSceneController_RestoredNpcKeepsConfiguredSpawnAsItsHome()
        {
            Object riverbend = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset");
            var serializedLocation = new SerializedObject(riverbend);
            SerializedProperty spawns = serializedLocation.FindProperty("initialCardSpawns");
            Object villageChiefDefinition = null;
            Vector3 configuredHome = Vector3.zero;
            for (int index = 0; index < spawns.arraySize; index++)
            {
                SerializedProperty spawn = spawns.GetArrayElementAtIndex(index);
                Object definition = spawn.FindPropertyRelative("definition").objectReferenceValue;
                var serializedCard = new SerializedObject(definition);
                if (serializedCard.FindProperty("displayName").stringValue != "村长")
                    continue;

                villageChiefDefinition = definition;
                configuredHome = spawn.FindPropertyRelative("position").vector3Value;
                break;
            }

            Component villageChief = CreateUninitializedCard(villageChiefDefinition, "Restored Village Chief");
            try
            {
                SetTestCardStackPosition(villageChief, configuredHome + new Vector3(0.8f, 0f, 0.4f));
                System.Type controllerType = FindType("CryingSnow.StackCraft.LocationSceneController");
                MethodInfo configure = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(method => method.Name == "ConfigureLocationCardBehaviours" &&
                        method.GetParameters().Length == 2);
                Assert.That(configure, Is.Not.Null,
                    "读档配置地点行为时需要地点定义，不能把 NPC 当前存档位置当作永久出生点");

                System.Type cardType = villageChief.GetType();
                System.Array cards = System.Array.CreateInstance(cardType, 1);
                cards.SetValue(villageChief, 0);
                configure.Invoke(null, new object[] { cards, riverbend });

                System.Type activityType = FindType("CryingSnow.StackCraft.LocationNpcActivity");
                Component activity = villageChief.GetComponent(activityType);
                Assert.That(activityType.GetProperty("HomePosition").GetValue(activity),
                    Is.EqualTo(configuredHome),
                    "NPC 读档后仍应围绕地点策划配置的出生点活动");
            }
            finally
            {
                DestroyTestCard(villageChief);
            }
        }

        [Test]
        public void RiverbendLocation_MissingInitialCardsCanBeMigratedWithoutDuplicates()
        {
            Object riverbend = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset");
            System.Type controllerType = FindType("CryingSnow.StackCraft.LocationSceneController");
            MethodInfo findMissing = controllerType.GetMethod(
                "FindMissingInitialCardSpawns",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(findMissing, Is.Not.Null,
                "旧存档中的河湾村也需要补齐新增卡牌，同时不能重复生成已有卡牌");

            string[] existing = { "riverbend-market", "riverbend-inn" };
            var missing = ((IEnumerable)findMissing.Invoke(null, new object[] { riverbend, existing }))
                .Cast<object>()
                .ToList();
            Assert.That(missing.Count, Is.EqualTo(8));

            var missingIds = missing.Select(spawn =>
            {
                object definition = spawn.GetType().GetProperty("Definition").GetValue(spawn);
                return (string)definition.GetType().GetProperty("Id").GetValue(definition);
            });
            Assert.That(missingIds, Does.Not.Contain("riverbend-market"));
            Assert.That(missingIds, Does.Not.Contain("riverbend-inn"));

            string[] allExisting =
            {
                "riverbend-market",
                "riverbend-blacksmith-shop",
                "riverbend-inn",
                "riverbend-village-chief",
                "riverbend-blacksmith",
                "riverbend-grocer",
                "riverbend-apothecary",
                "85e392d1882a4c61b5b2736e6fb64f4b"
            };
            var noneMissing = ((IEnumerable)findMissing.Invoke(
                    null,
                    new object[] { riverbend, allExisting }))
                .Cast<object>();
            Assert.That(noneMissing, Is.Empty);
        }

        [Test]
        public void RiverbendLocation_KeepsOriginalCardScaleAndUsesCompactBoard()
        {
            Object riverbend = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset");
            Assert.That(riverbend, Is.Not.Null);

            var serializedLocation = new SerializedObject(riverbend);
            Assert.That(serializedLocation.FindProperty("partyMemberScale"), Is.Null,
                "Location maps must not resize the original card prefab.");
            Vector2 mapSize = serializedLocation.FindProperty("mapSize").vector2Value;
            Assert.That(mapSize.x, Is.EqualTo(18.4f).Within(0.01f));
            Assert.That(mapSize.y, Is.EqualTo(10.35f).Within(0.01f));
            Assert.That(
                serializedLocation.FindProperty("cameraInitialDistance").floatValue,
                Is.EqualTo(7f).Within(0.01f),
                "The camera should frame the smaller board without scaling cards.");
            Assert.That(
                serializedLocation.FindProperty("partySpawnPosition").vector3Value,
                Is.EqualTo(new Vector3(0f, 0f, -0.48f)));
            Assert.That(
                serializedLocation.FindProperty("partyMemberSpacing").floatValue,
                Is.EqualTo(0.9f).Within(0.01f));

            SerializedProperty spawns = serializedLocation.FindProperty("initialCardSpawns");
            Assert.That(spawns.arraySize, Is.EqualTo(10));
            for (int index = 0; index < spawns.arraySize; index++)
            {
                SerializedProperty spawn = spawns.GetArrayElementAtIndex(index);
                Assert.That(spawn.FindPropertyRelative("scale"), Is.Null,
                    "Initial location cards must remain at the original one-to-one scale.");
            }

            System.Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
            Assert.That(cardType.GetMethod("SetPresentationScale"), Is.Null,
                "Card scaling was the source of stack and collider regressions and must be removed.");
        }

        [Test]
        public void CardManager_VillageNpcsAreInertButBabiesRemainSurvivalCharacters()
        {
            Object travelerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Object babyDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Baby.asset");
            Object villageChiefDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_VillageChief.asset");
            Component traveler = CreateUninitializedCard(travelerDefinition, "Player Traveler");
            Component baby = CreateUninitializedCard(babyDefinition, "Neutral Baby");
            Component villageChief = CreateUninitializedCard(villageChiefDefinition, "Neutral Village Chief");
            try
            {
                System.Type cardManagerType = FindType("CryingSnow.StackCraft.CardManager");
                MethodInfo getSurvivalCharacters = cardManagerType.GetMethod(
                    "GetSurvivalCharacters",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.That(getSurvivalCharacters, Is.Not.Null,
                    "地点 NPC 需要显式退出生存结算，不能简单按 Neutral 阵营排除婴儿");

                System.Type cardType = traveler.GetType();
                System.Array cards = System.Array.CreateInstance(cardType, 3);
                cards.SetValue(traveler, 0);
                cards.SetValue(baby, 1);
                cards.SetValue(villageChief, 2);
                var survivalCharacters = ((IEnumerable)getSurvivalCharacters.Invoke(
                        null,
                        new object[] { cards }))
                    .Cast<object>()
                    .ToList();

                Assert.That(survivalCharacters, Has.Count.EqualTo(2));
                Assert.That(survivalCharacters, Does.Contain(traveler));
                Assert.That(survivalCharacters, Does.Contain(baby));
                Assert.That(
                    survivalCharacters.Any(card => ReferenceEquals(card, villageChief)),
                    Is.False);
            }
            finally
            {
                DestroyTestCard(traveler);
                DestroyTestCard(baby);
                DestroyTestCard(villageChief);
            }
        }

        [Test]
        public void CardManager_StaticVillageCardsDoNotConsumePlayerCardCapacity()
        {
            Object travelerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Object marketDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Market.asset");
            Object villageChiefDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_VillageChief.asset");
            Component traveler = CreateUninitializedCard(travelerDefinition, "Player Traveler");
            Component market = CreateUninitializedCard(marketDefinition, "Static Market");
            Component villageChief = CreateUninitializedCard(villageChiefDefinition, "Static Village Chief");
            try
            {
                System.Type cardManagerType = FindType("CryingSnow.StackCraft.CardManager");
                MethodInfo getCapacityCards = cardManagerType.GetMethod(
                    "GetCardsCountingTowardLimit",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.That(getCapacityCards, Is.Not.Null,
                    "固定地点卡不能挤占玩家卡牌上限，否则进入河湾村会平白减少容量");

                System.Type cardType = traveler.GetType();
                System.Array cards = System.Array.CreateInstance(cardType, 3);
                cards.SetValue(traveler, 0);
                cards.SetValue(market, 1);
                cards.SetValue(villageChief, 2);
                var capacityCards = ((IEnumerable)getCapacityCards.Invoke(
                        null,
                        new object[] { cards }))
                    .Cast<object>()
                    .ToList();

                Assert.That(capacityCards, Has.Count.EqualTo(1));
                Assert.That(capacityCards[0], Is.SameAs(traveler));
            }
            finally
            {
                DestroyTestCard(traveler);
                DestroyTestCard(market);
                DestroyTestCard(villageChief);
            }
        }

        [Test]
        public void RiverbendLocation_UsesCompactWideBoardAndDedicatedSketchBackground()
        {
            const string scenePath = "Assets/StackCraft/Scenes/Location.unity";
            const string backgroundPath =
                "Assets/CardColony/Art/Backgrounds/RiverbendVillageBackground_v3.png";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            MonoBehaviour controller = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.LocationSceneController");
            var serializedController = new SerializedObject(controller);
            Object riverbend = serializedController.FindProperty("locationDefinitions")
                .GetArrayElementAtIndex(0)
                .objectReferenceValue;
            var serializedDefinition = new SerializedObject(riverbend);

            SerializedProperty mapSize = serializedDefinition.FindProperty("mapSize");
            Assert.That(mapSize, Is.Not.Null,
                "地点定义需要独立地图尺寸，未来地点才能复用同一场景而不共享固定大小");
            Assert.That(mapSize.vector2Value.x, Is.EqualTo(18.4f).Within(0.01f));
            Assert.That(mapSize.vector2Value.y, Is.EqualTo(10.35f).Within(0.01f));

            Texture2D expectedBackground = AssetDatabase.LoadAssetAtPath<Texture2D>(backgroundPath);
            Assert.That(expectedBackground, Is.Not.Null);
            var textureImporter = AssetImporter.GetAtPath(backgroundPath) as TextureImporter;
            Assert.That(textureImporter, Is.Not.Null);
            Assert.That(textureImporter.npotScale, Is.EqualTo(TextureImporterNPOTScale.None),
                "宽屏背景必须保留原始比例，不能被导入器强制缩放成方形尺寸");
            Assert.That(textureImporter.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(
                serializedDefinition.FindProperty("backgroundTexture").objectReferenceValue,
                Is.EqualTo(expectedBackground),
                "河湾村不能继续使用通用草地纹理");

            MonoBehaviour board = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName == "CryingSnow.StackCraft.Board");
            board.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(board, null);
            controller.GetType().GetField(
                    "activeDefinition",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, riverbend);
            controller.GetType().GetMethod(
                    "ApplyBackground",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { expectedBackground });

            Bounds worldBounds = (Bounds)board.GetType().GetProperty("WorldBounds").GetValue(board);
            Assert.That(worldBounds.size.x, Is.EqualTo(18.4f).Within(0.01f));
            Assert.That(worldBounds.size.z, Is.EqualTo(10.35f).Within(0.01f));
            Assert.That(board.GetComponent<SkinnedMeshRenderer>().enabled, Is.False,
                "地点背景接管桌面后应隐藏旧模板棋盘，避免边框与新地图尺寸不一致");

            Transform background = GameObject.Find("Background").transform;
            Assert.That(background.localScale.x, Is.EqualTo(1.84f).Within(0.01f));
            Assert.That(background.localScale.z, Is.EqualTo(1.035f).Within(0.01f));
        }

        [Test]
        public void RiverbendLocation_StartsZoomedOutAndAllowsWholeMapOverview()
        {
            const string scenePath = "Assets/StackCraft/Scenes/Location.unity";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            MonoBehaviour locationController = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.LocationSceneController");
            var serializedController = new SerializedObject(locationController);
            Object riverbend = serializedController.FindProperty("locationDefinitions")
                .GetArrayElementAtIndex(0)
                .objectReferenceValue;
            var serializedDefinition = new SerializedObject(riverbend);

            SerializedProperty cameraMinDistance =
                serializedDefinition.FindProperty("cameraMinDistance");
            SerializedProperty cameraMaxDistance =
                serializedDefinition.FindProperty("cameraMaxDistance");
            SerializedProperty cameraInitialDistance =
                serializedDefinition.FindProperty("cameraInitialDistance");
            SerializedProperty cameraZoomSpeed =
                serializedDefinition.FindProperty("cameraZoomSpeed");
            Assert.That(cameraMinDistance, Is.Not.Null,
                "地点定义需要独立缩放参数，不能继续共用模板相机的固定范围");
            Assert.That(cameraMaxDistance, Is.Not.Null);
            Assert.That(cameraInitialDistance, Is.Not.Null);
            Assert.That(cameraZoomSpeed, Is.Not.Null,
                "Each location needs its own mouse-wheel zoom sensitivity.");
            Assert.That(cameraMinDistance.floatValue, Is.EqualTo(3f).Within(0.01f));
            Assert.That(cameraMaxDistance.floatValue, Is.EqualTo(24f).Within(0.01f));
            Assert.That(cameraInitialDistance.floatValue, Is.EqualTo(7f).Within(0.01f));
            Assert.That(cameraZoomSpeed.floatValue, Is.EqualTo(3f).Within(0.01f));

            MonoBehaviour board = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName == "CryingSnow.StackCraft.Board");
            board.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(board, null);

            MonoBehaviour cameraController = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.CameraController");
            cameraController.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cameraController, null);

            locationController.GetType().GetField(
                    "activeDefinition",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(locationController, riverbend);
            Texture2D backgroundTexture = (Texture2D)serializedDefinition
                .FindProperty("backgroundTexture")
                .objectReferenceValue;
            locationController.GetType().GetMethod(
                    "ApplyBackground",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(locationController, new object[] { backgroundTexture });

            float minDistance = (float)cameraController.GetType().GetField(
                    "minDistance",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(cameraController);
            float maxDistance = (float)cameraController.GetType().GetField(
                    "maxDistance",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(cameraController);
            Assert.That(minDistance, Is.EqualTo(3f).Within(0.01f));
            Assert.That(maxDistance, Is.EqualTo(24f).Within(0.01f));
            float zoomSpeed = (float)cameraController.GetType().GetField(
                    "zoomSpeed",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(cameraController);
            Assert.That(zoomSpeed, Is.EqualTo(3f).Within(0.01f));

            Transform cameraTransform = (Transform)cameraController.GetType().GetField(
                    "cameraTransform",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(cameraController);
            var ground = new Plane(Vector3.up, Vector3.zero);
            Assert.That(
                ground.Raycast(new Ray(cameraTransform.position, cameraTransform.forward), out float distance),
                Is.True);
            Assert.That(distance, Is.EqualTo(7f).Within(0.1f),
                "进入河湾村时应先看到较大范围，而不是沿用模板的近距离视角");
            Assert.That(cameraTransform.GetComponent<Camera>().farClipPlane, Is.GreaterThanOrEqualTo(48f),
                "扩大缩放上限后必须同步提高远裁剪面，避免地图在远景被裁掉");

            Vector3 initialFocus = new Ray(cameraTransform.position, cameraTransform.forward)
                .GetPoint(distance);
            cameraController.GetType().GetMethod("ConfigureZoom").Invoke(
                cameraController,
                new object[] { 5f, 52f, 52f, 3f });
            Assert.That(
                ground.Raycast(new Ray(cameraTransform.position, cameraTransform.forward), out float overviewDistance),
                Is.True);
            Assert.That(overviewDistance, Is.EqualTo(52f).Within(0.1f));
            Vector3 overviewFocus = new Ray(cameraTransform.position, cameraTransform.forward)
                .GetPoint(overviewDistance);
            Assert.That(Vector3.Distance(initialFocus, overviewFocus), Is.LessThan(0.01f),
                "从初始视角拉到最远视角时不能漂移地图焦点");
        }

        [Test]
        public void CameraController_ZoomUsesRealCameraOffsetAndIgnoresInvalidGroundRay()
        {
            System.Type cameraControllerType = FindType("CryingSnow.StackCraft.CameraController");
            var root = new GameObject("Offset Camera Controller");
            var cameraObject = new GameObject("Offset Camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.localPosition = new Vector3(1f, 2f, 0.5f);
            cameraObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            root.transform.position = new Vector3(4f, 10f, -3f);
            Component cameraController = root.AddComponent(cameraControllerType);
            try
            {
                cameraControllerType.GetField(
                        "cameraTransform",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(cameraController, cameraObject.transform);
                cameraControllerType.GetMethod(
                        "Awake",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(cameraController, null);

                var ground = new Plane(Vector3.up, Vector3.zero);
                Ray originalRay = new Ray(cameraObject.transform.position, cameraObject.transform.forward);
                Assert.That(ground.Raycast(originalRay, out float originalDistance), Is.True);
                Vector3 originalFocus = originalRay.GetPoint(originalDistance);

                cameraControllerType.GetMethod("ConfigureZoom").Invoke(
                    cameraController,
                    new object[] { 5f, 80f, 32f, 3f });
                Ray configuredRay = new Ray(cameraObject.transform.position, cameraObject.transform.forward);
                Assert.That(ground.Raycast(configuredRay, out float configuredDistance), Is.True);
                Assert.That(configuredDistance, Is.EqualTo(32f).Within(0.1f));
                Assert.That(
                    Vector3.Distance(originalFocus, configuredRay.GetPoint(configuredDistance)),
                    Is.LessThan(0.01f),
                    "相机子节点有偏移时，缩放仍必须围绕真实地面焦点进行");

                cameraObject.transform.rotation = Quaternion.identity;
                Vector3 positionBeforeInvalidRay = root.transform.position;
                cameraControllerType.GetMethod("ConfigureZoom").Invoke(
                    cameraController,
                    new object[] { 5f, 80f, 60f, 3f });
                Assert.That(root.transform.position, Is.EqualTo(positionBeforeInvalidRay),
                    "相机不朝向地面时不能为了套用缩放距离而把控制器移动到地下");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CameraController_ZoomDistanceUsesSensitivityAndClampsAtLimits()
        {
            System.Type cameraControllerType = FindType("CryingSnow.StackCraft.CameraController");
            var root = new GameObject("Zoom Distance Controller");
            Component cameraController = root.AddComponent(cameraControllerType);
            try
            {
                cameraControllerType.GetField(
                        "minDistance",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(cameraController, 5f);
                cameraControllerType.GetField(
                        "maxDistance",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(cameraController, 60f);
                cameraControllerType.GetField(
                        "zoomSpeed",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(cameraController, 3f);

                MethodInfo calculateZoomDistance = cameraControllerType.GetMethod(
                    "CalculateZoomDistance",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(calculateZoomDistance, Is.Not.Null);
                Assert.That(
                    (float)calculateZoomDistance.Invoke(cameraController, new object[] { 26f, 1f }),
                    Is.EqualTo(23f).Within(0.01f));
                Assert.That(
                    (float)calculateZoomDistance.Invoke(cameraController, new object[] { 59f, -1f }),
                    Is.EqualTo(60f).Within(0.01f),
                    "A large wheel step must land on the maximum instead of being discarded.");
                Assert.That(
                    (float)calculateZoomDistance.Invoke(cameraController, new object[] { 6f, 1f }),
                    Is.EqualTo(5f).Within(0.01f),
                    "A large wheel step must land on the minimum instead of being discarded.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MainScene_ConfiguresSidebarDetailsForEveryWorldMapLocation()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component =>
                    component.GetType().FullName == "CryingSnow.StackCraft.WorldMapBootstrap");
            Assert.That(bootstrap, Is.Not.Null);

            var serialized = new SerializedObject(bootstrap);
            SerializedProperty spawns = serialized.FindProperty("locationSpawns");
            SerializedProperty details = serialized.FindProperty("locationDetails");
            Assert.That(details, Is.Not.Null);
            Assert.That(details.arraySize, Is.EqualTo(spawns.arraySize));

            bool includesHerbs = false;
            for (int index = 0; index < details.arraySize; index++)
            {
                SerializedProperty entry = details.GetArrayElementAtIndex(index);
                Assert.That(entry.FindPropertyRelative("locationType").stringValue, Is.Not.Empty);
                Assert.That(entry.FindPropertyRelative("dangerLevel").intValue, Is.GreaterThanOrEqualTo(1));
                Assert.That(entry.FindPropertyRelative("travelTime").stringValue, Is.Not.Empty);
                Assert.That(entry.FindPropertyRelative("description").stringValue, Is.Not.Empty);
                SerializedProperty resources = entry.FindPropertyRelative("possibleResources");
                Assert.That(resources.arraySize, Is.GreaterThan(0));
                for (int resourceIndex = 0; resourceIndex < resources.arraySize; resourceIndex++)
                {
                    includesHerbs |= resources.GetArrayElementAtIndex(resourceIndex)
                        .stringValue.Contains("草药");
                }
            }

            Assert.That(includesHerbs, Is.True, "低语森林地点详情需要显示草药资源线索");
        }

        [Test]
        public void MainScene_WorldMapBackgroundSharesTheNativeCardPlane()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            GameObject background = GameObject.Find("Background");
            MonoBehaviour board = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().FullName == "CryingSnow.StackCraft.Board");
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().FullName == "CryingSnow.StackCraft.WorldMapBootstrap");

            Assert.That(background, Is.Not.Null);
            Assert.That(board, Is.Not.Null);
            Assert.That(bootstrap, Is.Not.Null);

            MethodInfo boardAwake = board.GetType().GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(boardAwake, Is.Not.Null);
            boardAwake.Invoke(board, null);

            background.transform.position = new Vector3(7f, -10f, 9f);
            MethodInfo applyBackground = bootstrap.GetType().GetMethod(
                "ApplyWorldMapBackground",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(applyBackground, Is.Not.Null);
            applyBackground.Invoke(bootstrap, null);

            Assert.That(background.transform.position.x, Is.EqualTo(board.transform.position.x).Within(0.001f));
            Assert.That(background.transform.position.z, Is.EqualTo(board.transform.position.z).Within(0.001f));
            Assert.That(
                background.transform.position.y,
                Is.EqualTo(board.transform.position.y - 0.01f).Within(0.001f),
                "地图与原生卡牌桌面相距过远，透视相机移动时会产生明显视差");
        }

        [Test]
        public void WorldMapBackground_KeepsSceneFallbackWhenBoardIsUnavailable()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            GameObject background = GameObject.Find("Background");
            MonoBehaviour board = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().FullName == "CryingSnow.StackCraft.Board");
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().FullName == "CryingSnow.StackCraft.WorldMapBootstrap");

            Assert.That(background, Is.Not.Null);
            Assert.That(board, Is.Not.Null);
            Assert.That(bootstrap, Is.Not.Null);

            Object.DestroyImmediate(board.gameObject);
            Vector3 fallbackPosition = new(3f, -0.06f, 4f);
            background.transform.position = fallbackPosition;
            MethodInfo applyBackground = bootstrap.GetType().GetMethod(
                "ApplyWorldMapBackground",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(applyBackground, Is.Not.Null);
            Assert.That(() => applyBackground.Invoke(bootstrap, null), Throws.Nothing);
            Assert.That(background.transform.position, Is.EqualTo(fallbackPosition));
        }

        [Test]
        public void MainScene_NewGameUsesNativeWorldMapInsteadOfStarterPack()
        {
            const string scenePath = "Assets/StackCraft/Scenes/Main.unity";
            const string starterPackGuid = "51c2d41ab5d413649adfbe0e3bb29d85";
            string sceneYaml = File.ReadAllText(scenePath);

            Assert.That(
                sceneYaml,
                Does.Not.Contain(starterPackGuid),
                "新游戏仍会生成旧 Starter Pack，因此不会进入世界地图玩法");

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component =>
                    component.GetType().FullName == "CryingSnow.StackCraft.WorldMapBootstrap");

            Assert.That(bootstrap, Is.Not.Null, "Main 场景必须挂载原生卡牌世界地图启动器");
        }

        [Test]
        public void WorldMapBootstrap_DefinesLocationsSinglePartyAndTravelRoutes()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component =>
                    component.GetType().FullName == "CryingSnow.StackCraft.WorldMapBootstrap");
            Assert.That(bootstrap, Is.Not.Null);

            var serialized = new SerializedObject(bootstrap);
            SerializedProperty locations = serialized.FindProperty("locationSpawns");
            SerializedProperty party = serialized.FindProperty("partyDefinition");
            SerializedProperty routes = serialized.FindProperty("routes");
            SerializedProperty mapTexture = serialized.FindProperty("worldMapTexture");
            SerializedProperty mapShader = serialized.FindProperty("worldMapShader");

            Assert.That(locations, Is.Not.Null);
            Assert.That(locations.arraySize, Is.GreaterThanOrEqualTo(6));
            Assert.That(party, Is.Not.Null);
            Assert.That(party.objectReferenceValue, Is.Not.Null);
            Assert.That(routes, Is.Not.Null);
            Assert.That(routes.arraySize, Is.GreaterThanOrEqualTo(7));
            Assert.That(
                bootstrap.GetType().GetMethod("SpawnJobStack", BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null,
                "世界地图只能生成一张小队卡，不能继续生成战士、游侠和法师职业堆");
            Assert.That(mapTexture, Is.Not.Null);
            Assert.That(mapTexture.objectReferenceValue, Is.Not.Null, "原生卡牌下方需要世界地图底图");
            Assert.That(
                AssetDatabase.GetAssetPath(mapTexture.objectReferenceValue),
                Is.EqualTo("Assets/CardColony/Art/Backgrounds/WorldMapBackground_v4_MinimalRoutes.png"),
                "世界地图场景必须引用已确认的简笔画路线底图");
            const string mapTexturePath =
                "Assets/CardColony/Art/Backgrounds/WorldMapBackground_v4_MinimalRoutes.png";
            var mapImporter = AssetImporter.GetAtPath(mapTexturePath) as TextureImporter;
            Assert.That(mapImporter, Is.Not.Null);
            Assert.That(mapImporter.mipmapEnabled, Is.False, "简笔路线底图不能因 Mipmap 采样而变糊");
            Assert.That(mapImporter.npotScale, Is.EqualTo(TextureImporterNPOTScale.None),
                "必须保留地图原始 1672×941 比例，不能重采样为二次幂尺寸");
            Assert.That(mapImporter.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(mapShader, Is.Not.Null);
            Assert.That(mapShader.objectReferenceValue, Is.Not.Null, "地图材质必须明确引用 URP Shader，不能运行时猜测");

            for (int index = 0; index < locations.arraySize; index++)
            {
                Object definition = locations.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("definition").objectReferenceValue;
                Assert.That(definition, Is.Not.Null, $"地点 {index} 缺少原生 CardDefinition");
                Assert.That(definition.GetType().Name, Is.Not.EqualTo("PackDefinition"));
            }

            MethodInfo areConnected = bootstrap.GetType().GetMethod("AreLocationsConnected");
            Assert.That(areConnected, Is.Not.Null);
            Assert.That(areConnected.Invoke(bootstrap, new object[] { 0, 1 }), Is.True);
            Assert.That(areConnected.Invoke(bootstrap, new object[] { 0, 5 }), Is.False);
        }

        [Test]
        public void Board_WorldMapBoundsOverrideMatchesTheVisibleMapArea()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour board = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().FullName == "CryingSnow.StackCraft.Board");
            Assert.That(board, Is.Not.Null);

            MethodInfo setOverride = board.GetType().GetMethod("SetWorldBoundsOverride");
            Assert.That(setOverride, Is.Not.Null);

            Bounds expected = new(Vector3.zero, new Vector3(20f, 0.1f, 11.25f));
            setOverride.Invoke(board, new object[] { expected });
            Bounds actual = (Bounds)board.GetType().GetProperty("WorldBounds").GetValue(board);

            Assert.That(actual.center, Is.EqualTo(expected.center));
            Assert.That(actual.size, Is.EqualTo(expected.size));
        }

        [Test]
        public void WorldMapLocationSpawns_AlignWithTheBakedRouteAnchors()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            GameObject background = GameObject.Find("Background");
            MonoBehaviour board = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().FullName == "CryingSnow.StackCraft.Board");
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().FullName == "CryingSnow.StackCraft.WorldMapBootstrap");
            Assert.That(background, Is.Not.Null);
            Assert.That(board, Is.Not.Null);
            Assert.That(bootstrap, Is.Not.Null);

            MethodInfo boardAwake = board.GetType().GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo applyBackground = bootstrap.GetType().GetMethod(
                "ApplyWorldMapBackground",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(boardAwake, Is.Not.Null);
            Assert.That(applyBackground, Is.Not.Null);
            boardAwake.Invoke(board, null);
            applyBackground.Invoke(bootstrap, null);

            Bounds mapBounds = background.GetComponent<Renderer>().bounds;
            Assert.That(mapBounds.size.x, Is.EqualTo(20f).Within(0.01f));
            Assert.That(mapBounds.size.z, Is.EqualTo(11.25f).Within(0.01f));

            var serialized = new SerializedObject(bootstrap);
            SerializedProperty locations = serialized.FindProperty("locationSpawns");
            SerializedProperty mapTexture = serialized.FindProperty("worldMapTexture");
            var texture = mapTexture.objectReferenceValue as Texture2D;
            Assert.That(texture, Is.Not.Null);
            Assert.That(
                (float)texture.width / texture.height,
                Is.EqualTo(mapBounds.size.x / mapBounds.size.z).Within(0.002f));

            Vector2[] anchorUvs =
            {
                new(0.2225f, 0.3222f),
                new(0.2475f, 0.7267f),
                new(0.4550f, 0.7267f),
                new(0.4575f, 0.5133f),
                new(0.6250f, 0.2156f),
                new(0.8425f, 0.3889f),
            };

            Assert.That(locations.arraySize, Is.EqualTo(anchorUvs.Length));
            for (int index = 0; index < anchorUvs.Length; index++)
            {
                Vector3 actual = locations.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("position").vector3Value;
                Vector3 expected = new(
                    Mathf.Lerp(mapBounds.min.x, mapBounds.max.x, anchorUvs[index].x),
                    0f,
                    Mathf.Lerp(mapBounds.min.z, mapBounds.max.z, anchorUvs[index].y));
                Assert.That(
                    Vector3.Distance(actual, expected),
                    Is.LessThan(0.05f),
                    $"地点 {index} 没有覆盖新地图的路线落点");
            }
        }

        [Test]
        public void CardController_RejectsDraggingWhenItsWorldMapStackIsLocked()
        {
            var gameObject = new GameObject("Locked world map card");
            try
            {
                System.Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
                System.Type controllerType = FindType("CryingSnow.StackCraft.CardController");
                System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
                Assert.That(cardType, Is.Not.Null);
                Assert.That(controllerType, Is.Not.Null);
                Assert.That(stackType, Is.Not.Null);

                Component card = gameObject.AddComponent(cardType);
                Component controller = gameObject.AddComponent(controllerType);
                object stack = System.Activator.CreateInstance(stackType, nonPublic: true);
                cardType.GetProperty("Stack").SetValue(card, stack);
                MethodInfo controllerAwake = controllerType.GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(controllerAwake, Is.Not.Null);
                controllerAwake.Invoke(controller, null);
                PropertyInfo canBeDragged = controller.GetType().GetProperty("CanBeDragged");

                Assert.That(canBeDragged, Is.Not.Null);
                Assert.That(canBeDragged.GetValue(controller), Is.True);

                stackType.GetProperty("IsLocked").SetValue(stack, true);
                Assert.That(canBeDragged.GetValue(controller), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void WorldMapBootstrap_ConfiguresLocationsAsLockedAndPartyAsMovable()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().FullName == "CryingSnow.StackCraft.WorldMapBootstrap");
            Assert.That(bootstrap, Is.Not.Null);

            var serialized = new SerializedObject(bootstrap);
            SerializedProperty locations = serialized.FindProperty("locationSpawns");
            SerializedProperty partyProperty = serialized.FindProperty("partyDefinition");
            Assert.That(locations, Is.Not.Null);
            Assert.That(partyProperty, Is.Not.Null);

            Object locationDefinition = locations
                .GetArrayElementAtIndex(0).FindPropertyRelative("definition").objectReferenceValue;
            Object partyDefinition = partyProperty.objectReferenceValue;
            Assert.That(locationDefinition, Is.Not.Null);
            Assert.That(partyDefinition, Is.Not.Null);
            MethodInfo configure = bootstrap.GetType().GetMethod("ConfigureSpawnedCard");
            Assert.That(configure, Is.Not.Null);

            Component location = CreateUninitializedCard(locationDefinition, "Test Location");
            Component party = CreateUninitializedCard(partyDefinition, "Test Party");
            try
            {
                configure.Invoke(bootstrap, new object[] { location });
                configure.Invoke(bootstrap, new object[] { party });

                object locationStack = location.GetType().GetProperty("Stack").GetValue(location);
                object partyStack = party.GetType().GetProperty("Stack").GetValue(party);
                Assert.That(locationStack.GetType().GetProperty("IsLocked").GetValue(locationStack), Is.True);
                Assert.That(location.GetComponent("WorldMapLocation"), Is.Not.Null);
                Assert.That(partyStack.GetType().GetProperty("IsLocked").GetValue(partyStack), Is.False);
                Assert.That(party.GetComponent("WorldMapPartyController"), Is.Not.Null);
            }
            finally
            {
                DestroyTestCard(location);
                DestroyTestCard(party);
            }
        }

        [Test]
        public void WorldMapLocation_AttachesPartyLikeEquipmentAndRestoresItWhenDetached()
        {
            System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
            Assert.That(locationType, Is.Not.Null);

            Component locationCard = CreateUninitializedCard(null, "Equipment-style Location");
            Component partyCard = CreateUninitializedCard(null, "Docked Party");
            Component location = locationCard.gameObject.AddComponent(locationType);
            try
            {
                locationType.GetMethod("Initialize")?.Invoke(location, new object[] { 0, locationCard });

                MethodInfo attach = locationType.GetMethod("AttachParty");
                MethodInfo detach = locationType.GetMethod("DetachParty");
                PropertyInfo dockedParty = locationType.GetProperty("DockedParty");
                Assert.That(attach, Is.Not.Null, "地点卡需要提供类似装备槽的驻扎挂载入口");
                Assert.That(detach, Is.Not.Null, "小队开始旅行时需要能从地点卡脱离");
                Assert.That(dockedParty, Is.Not.Null);

                Vector3 localDockPosition = new(0f, 0.01f, -0.55f);
                attach.Invoke(location, new object[] { partyCard, localDockPosition, 0.78f, true });

                Assert.That(dockedParty.GetValue(location), Is.SameAs(partyCard));
                Assert.That(partyCard.transform.parent, Is.SameAs(locationCard.transform));
                Assert.That(partyCard.transform.localPosition, Is.EqualTo(localDockPosition));
                Assert.That(partyCard.transform.localScale, Is.EqualTo(Vector3.one * 0.78f));

                detach.Invoke(location, new object[] { partyCard });

                Assert.That(dockedParty.GetValue(location), Is.Null);
                Assert.That(partyCard.transform.parent, Is.Null);
                Assert.That(partyCard.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                if (partyCard != null && partyCard.transform.parent != null)
                    partyCard.transform.SetParent(null, true);
                DestroyTestCard(locationCard);
                DestroyTestCard(partyCard);
            }
        }

        [Test]
        public void WorldMapLocation_PersonSlotCollapsesExpandsAndFloatsLikeEquipmentPanel()
        {
            System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
            System.Type personSlotType = FindType("CryingSnow.StackCraft.WorldMapPersonSlot");
            Assert.That(locationType, Is.Not.Null);
            Assert.That(personSlotType, Is.Not.Null, "地点卡需要独立的人物槽组件管理驻扎人物的显示与交互");

            Component locationCard = CreateUninitializedCard(null, "Location With Person Slot");
            Component partyCard = CreateUninitializedCard(null, "Person Slot Occupant");
            Component location = locationCard.gameObject.AddComponent(locationType);
            try
            {
                locationType.GetMethod("Initialize").Invoke(location, new object[] { 0, locationCard });
                Component personSlot = locationCard.GetComponent(personSlotType);
                Assert.That(personSlot, Is.Not.Null);
                Assert.That(location, Is.InstanceOf<IPointerClickHandler>(),
                    "地点卡必须能通过点击统一控制选中状态和人物槽展开");

                Vector3 dock = new(0f, 0.01f, -0.55f);
                locationType.GetMethod("AttachParty").Invoke(
                    location,
                    new object[] { partyCard, dock, 0.78f, true });

                PropertyInfo isExpanded = personSlotType.GetProperty("IsExpanded");
                PropertyInfo occupant = personSlotType.GetProperty("Occupant");
                MethodInfo hideCards = personSlotType.GetMethod("HideCards");
                MethodInfo showCards = personSlotType.GetMethod("ShowCards");
                MethodInfo animateCards = personSlotType.GetMethod(
                    "AnimateCards",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(isExpanded, Is.Not.Null);
                Assert.That(occupant, Is.Not.Null);
                Assert.That(hideCards, Is.Not.Null);
                Assert.That(showCards, Is.Not.Null);
                Assert.That(animateCards, Is.Not.Null);
                Assert.That(occupant.GetValue(personSlot), Is.SameAs(partyCard));

                Renderer partyRenderer = partyCard.GetComponent<Renderer>();
                Collider partyCollider = partyCard.GetComponent<Collider>();
                Assert.That(isExpanded.GetValue(personSlot), Is.False);
                Assert.That(partyRenderer.enabled, Is.False,
                    "人物卡进入地点后必须立即收进人物槽，不能依赖不确定的 Start 执行顺序");
                Assert.That(partyCollider.enabled, Is.False, "隐藏人物卡不能继续拦截地点卡点击");
                Assert.That(partyCard.transform.localPosition, Is.EqualTo(dock),
                    "收起时人物卡应回到人物槽基准位置");

                var click = new PointerEventData(null)
                {
                    button = PointerEventData.InputButton.Left,
                };
                ((IPointerClickHandler)location).OnPointerClick(click);
                Assert.That(isExpanded.GetValue(personSlot), Is.True);
                Assert.That(partyRenderer.enabled, Is.True);
                Assert.That(partyCollider.enabled, Is.True);

                animateCards.Invoke(personSlot, null);
                Vector3 animatedOffset = partyCard.transform.localPosition - dock;
                Assert.That(animatedOffset.y, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(animatedOffset.magnitude, Is.LessThanOrEqualTo(0.15f),
                    "展开后的人物卡只能在人物槽附近轻微漂浮");

                ((IPointerClickHandler)location).OnPointerClick(click);
                Assert.That(isExpanded.GetValue(personSlot), Is.False);
            }
            finally
            {
                if (partyCard != null && partyCard.transform.parent != null)
                    partyCard.transform.SetParent(null, true);
                DestroyTestCard(locationCard);
                DestroyTestCard(partyCard);
            }
        }

        [Test]
        public void WorldMapLocation_SelectedStateLiftsAndOutlinesLocationAndDockedParty()
        {
            System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
            Component locationCard = CreateUninitializedCard(null, "Selectable Location");
            Component partyCard = CreateUninitializedCard(null, "Outlined Docked Party");
            Component location = locationCard.gameObject.AddComponent(locationType);
            try
            {
                locationType.GetMethod("Initialize").Invoke(location, new object[] { 0, locationCard });
                locationType.GetMethod("AttachParty").Invoke(
                    location,
                    new object[] { partyCard, new Vector3(0f, 0.01f, -0.55f), 0.78f, true });

                PropertyInfo isSelected = locationType.GetProperty("IsSelected");
                MethodInfo setSelected = locationType.GetMethod(
                    "SetSelected",
                    new[] { typeof(bool), typeof(bool) });
                Assert.That(isSelected, Is.Not.Null);
                Assert.That(setSelected, Is.Not.Null,
                    "地点卡需要可复用的选中状态入口来同步动画、地点框和人物框");

                Vector3 restingPosition = locationCard.transform.localPosition;
                setSelected.Invoke(location, new object[] { true, true });

                Assert.That(isSelected.GetValue(location), Is.True);
                Assert.That(locationCard.transform.localPosition.y,
                    Is.GreaterThan(restingPosition.y + 0.01f),
                    "地点选中后应从地图平面提起一点");
                Assert.That(locationCard.transform.Find("Highlight"), Is.Not.Null,
                    "地点选中后必须创建外轮廓");
                Assert.That(locationCard.transform.Find("Highlight").gameObject.activeSelf, Is.True);
                Assert.That(partyCard.transform.Find("Highlight"), Is.Not.Null,
                    "地点内驻扎的人物卡需要同步获得外轮廓");
                Assert.That(partyCard.transform.Find("Highlight").gameObject.activeSelf, Is.True);
                Assert.That(partyCard.GetComponent<Renderer>().enabled, Is.True,
                    "选中地点时人物槽应展开，确保人物框与人物卡一起可见");

                setSelected.Invoke(location, new object[] { false, true });
                Assert.That(isSelected.GetValue(location), Is.False);
                Assert.That(locationCard.transform.localPosition, Is.EqualTo(restingPosition));
                Assert.That(locationCard.transform.Find("Highlight").gameObject.activeSelf, Is.True,
                    "小队驻扎地点即使没有被选中，也必须保留常驻外框");
                Assert.That(partyCard.transform.Find("Highlight").gameObject.activeSelf, Is.False);
                Assert.That(partyCard.GetComponent<Renderer>().enabled, Is.False);
            }
            finally
            {
                if (partyCard != null && partyCard.transform.parent != null)
                    partyCard.transform.SetParent(null, true);
                DestroyTestCard(locationCard);
                DestroyTestCard(partyCard);
            }
        }

        [Test]
        public void WorldMapLocation_OccupiedLocationUsesDistinctPersistentOutlineColor()
        {
            System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
            Component locationCard = CreateUninitializedCard(null, "Occupied Location Outline");
            Component partyCard = CreateUninitializedCard(null, "Occupying Party");
            Component location = locationCard.gameObject.AddComponent(locationType);
            try
            {
                locationType.GetMethod("Initialize").Invoke(location, new object[] { 0, locationCard });
                locationType.GetMethod("AttachParty").Invoke(
                    location,
                    new object[] { partyCard, new Vector3(0f, 0.01f, -0.55f), 0.78f, true });

                Transform highlight = locationCard.transform.Find("Highlight");
                Assert.That(highlight, Is.Not.Null, "小队驻扎后地点卡必须立即创建常驻外框");
                Assert.That(highlight.gameObject.activeSelf, Is.True);

                var propertyBlock = new MaterialPropertyBlock();
                Renderer outlineRenderer = highlight.GetComponent<Renderer>();
                outlineRenderer.GetPropertyBlock(propertyBlock);
                int outlineColorId = Shader.PropertyToID("_OutlineColor");
                Color occupiedColor = propertyBlock.GetColor(outlineColorId);

                MethodInfo setSelected = locationType.GetMethod(
                    "SetSelected",
                    new[] { typeof(bool), typeof(bool) });
                setSelected.Invoke(location, new object[] { true, true });
                outlineRenderer.GetPropertyBlock(propertyBlock);
                Color selectedColor = propertyBlock.GetColor(outlineColorId);
                Assert.That(Vector4.Distance(occupiedColor, selectedColor), Is.GreaterThan(0.1f),
                    "驻扎状态必须使用与普通选中状态不同的外框颜色");

                setSelected.Invoke(location, new object[] { false, true });
                outlineRenderer.GetPropertyBlock(propertyBlock);
                Assert.That(highlight.gameObject.activeSelf, Is.True);
                Assert.That(
                    Vector4.Distance(occupiedColor, propertyBlock.GetColor(outlineColorId)),
                    Is.LessThan(0.001f),
                    "取消选中后应恢复驻扎状态颜色，而不是关闭外框");

                locationType.GetMethod("DetachParty").Invoke(location, new object[] { partyCard });
                Assert.That(highlight.gameObject.activeSelf, Is.False,
                    "小队离开后，旧地点不能继续显示驻扎外框");
            }
            finally
            {
                if (partyCard != null && partyCard.transform.parent != null)
                    partyCard.transform.SetParent(null, true);
                DestroyTestCard(locationCard);
                DestroyTestCard(partyCard);
            }
        }

        [Test]
        public void WorldMapLocation_SelectionIgnoresEmptyMapAndCancelsForOtherCard()
        {
            System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
            Component locationCard = CreateUninitializedCard(null, "Persistent Selection Location");
            Component otherCard = CreateUninitializedCard(null, "Other Clicked Card");
            Component location = locationCard.gameObject.AddComponent(locationType);
            try
            {
                locationType.GetMethod("Initialize").Invoke(location, new object[] { 0, locationCard });
                MethodInfo setSelected = locationType.GetMethod(
                    "SetSelected",
                    new[] { typeof(bool), typeof(bool) });
                MethodInfo notifyCardClicked = locationType.GetMethod(
                    "NotifyCardClicked",
                    BindingFlags.Public | BindingFlags.Static);
                PropertyInfo isSelected = locationType.GetProperty("IsSelected");
                Assert.That(notifyCardClicked, Is.Not.Null,
                    "地点选择需要统一处理卡牌点击，同时忽略空白地图点击");

                setSelected.Invoke(location, new object[] { true, true });
                notifyCardClicked.Invoke(null, new object[] { null });
                Assert.That(isSelected.GetValue(location), Is.True,
                    "点击空白地图不能取消当前地点选择");

                notifyCardClicked.Invoke(null, new object[] { otherCard });
                Assert.That(isSelected.GetValue(location), Is.False,
                    "点击其他卡牌必须取消当前地点选择");
            }
            finally
            {
                DestroyTestCard(locationCard);
                DestroyTestCard(otherCard);
            }
        }

        [Test]
        public void CardController_ClickingAnotherCardCancelsWorldMapSelection()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            System.Type inputManagerType = FindType("CryingSnow.StackCraft.InputManager");
            System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
            System.Type controllerType = FindType("CryingSnow.StackCraft.CardController");
            var inputObject = new GameObject("Selection Input Manager");
            Component locationCard = CreateUninitializedCard(null, "Selected Location Before Other Card Click");
            Component otherCard = CreateUninitializedCard(null, "Locked Other Card");
            try
            {
                Component inputManager = inputObject.AddComponent(inputManagerType);
                inputManagerType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(inputManager, null);

                Component location = locationCard.gameObject.AddComponent(locationType);
                locationType.GetMethod("Initialize").Invoke(location, new object[] { 0, locationCard });
                locationType.GetMethod("SetSelected", new[] { typeof(bool), typeof(bool) })
                    .Invoke(location, new object[] { true, true });

                object otherStack = otherCard.GetType().GetProperty("Stack").GetValue(otherCard);
                otherStack.GetType().GetProperty("IsLocked").SetValue(otherStack, true);
                Component controller = otherCard.gameObject.AddComponent(controllerType);
                controllerType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);

                ((IPointerDownHandler)controller).OnPointerDown(new PointerEventData(null)
                {
                    button = PointerEventData.InputButton.Left,
                });

                Assert.That(locationType.GetProperty("IsSelected").GetValue(location), Is.False,
                    "即使另一张卡不可拖动，点击它也必须取消当前地点选择");
            }
            finally
            {
                Object.DestroyImmediate(inputObject);
                DestroyTestCard(locationCard);
                DestroyTestCard(otherCard);
            }
        }

        [Test]
        public void WorldMapLocation_ClickSelectionMovesSelectionToTheNewLocation()
        {
            System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
            Component firstCard = CreateUninitializedCard(null, "First Selectable Location");
            Component secondCard = CreateUninitializedCard(null, "Second Selectable Location");
            Component first = firstCard.gameObject.AddComponent(locationType);
            Component second = secondCard.gameObject.AddComponent(locationType);
            try
            {
                locationType.GetMethod("Initialize").Invoke(first, new object[] { 0, firstCard });
                locationType.GetMethod("Initialize").Invoke(second, new object[] { 1, secondCard });
                Assert.That(first, Is.InstanceOf<IPointerClickHandler>());
                Assert.That(second, Is.InstanceOf<IPointerClickHandler>());

                MethodInfo setSelected = locationType.GetMethod(
                    "SetSelected",
                    new[] { typeof(bool), typeof(bool) });
                PropertyInfo isSelected = locationType.GetProperty("IsSelected");
                setSelected.Invoke(first, new object[] { true, true });

                var click = new PointerEventData(null)
                {
                    button = PointerEventData.InputButton.Left,
                };
                ((IPointerClickHandler)second).OnPointerClick(click);

                Assert.That(isSelected.GetValue(first), Is.False,
                    "选择新地点时旧地点必须取消选中");
                Assert.That(isSelected.GetValue(second), Is.True);
            }
            finally
            {
                DestroyTestCard(firstCard);
                DestroyTestCard(secondCard);
            }
        }

        [Test]
        public void WorldMapLocation_SelectedPersonOutlineMovesToReplacementOccupant()
        {
            System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
            Component locationCard = CreateUninitializedCard(null, "Selected Replacement Location");
            Component firstParty = CreateUninitializedCard(null, "Previous Outlined Party");
            Component secondParty = CreateUninitializedCard(null, "Replacement Outlined Party");
            Component location = locationCard.gameObject.AddComponent(locationType);
            try
            {
                locationType.GetMethod("Initialize").Invoke(location, new object[] { 0, locationCard });
                MethodInfo attach = locationType.GetMethod("AttachParty");
                MethodInfo setSelected = locationType.GetMethod(
                    "SetSelected",
                    new[] { typeof(bool), typeof(bool) });
                Vector3 dock = new(0f, 0.01f, -0.55f);

                attach.Invoke(location, new object[] { firstParty, dock, 0.78f, true });
                setSelected.Invoke(location, new object[] { true, true });
                attach.Invoke(location, new object[] { secondParty, dock, 0.78f, true });

                Assert.That(firstParty.transform.Find("Highlight").gameObject.activeSelf, Is.False,
                    "人物槽替换成员时必须关闭旧人物的驻扎框");
                Assert.That(secondParty.transform.Find("Highlight"), Is.Not.Null);
                Assert.That(secondParty.transform.Find("Highlight").gameObject.activeSelf, Is.True,
                    "新驻扎人物应接管选中框");
            }
            finally
            {
                if (firstParty != null && firstParty.transform.parent != null)
                    firstParty.transform.SetParent(null, true);
                if (secondParty != null && secondParty.transform.parent != null)
                    secondParty.transform.SetParent(null, true);
                DestroyTestCard(locationCard);
                DestroyTestCard(firstParty);
                DestroyTestCard(secondParty);
            }
        }

        [Test]
        public void WorldMapPartyController_DetachesFromPersonSlotWhenDragStarts()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component =>
                    component.GetType().FullName == "CryingSnow.StackCraft.WorldMapBootstrap");
            Assert.That(bootstrap, Is.Not.Null);

            var serialized = new SerializedObject(bootstrap);
            SerializedProperty locationSpawns = serialized.FindProperty("locationSpawns");
            SerializedProperty partyDefinition = serialized.FindProperty("partyDefinition");
            SerializedProperty initialLocation = serialized.FindProperty("initialPartyLocationIndex");
            MethodInfo configure = bootstrap.GetType().GetMethod("ConfigureSpawnedCard");
            var locations = new List<Component>();
            Component party = null;
            try
            {
                for (int index = 0; index < locationSpawns.arraySize; index++)
                {
                    SerializedProperty spawn = locationSpawns.GetArrayElementAtIndex(index);
                    Component locationCard = CreateUninitializedCard(
                        spawn.FindPropertyRelative("definition").objectReferenceValue,
                        $"Drag Location {index}");
                    SetTestCardStackPosition(
                        locationCard,
                        spawn.FindPropertyRelative("position").vector3Value);
                    configure.Invoke(bootstrap, new object[] { locationCard });
                    locations.Add(locationCard);
                }

                int startIndex = initialLocation.intValue;
                party = CreateUninitializedCard(partyDefinition.objectReferenceValue, "Dragged Party");
                SetTestCardStackPosition(
                    party,
                    locationSpawns.GetArrayElementAtIndex(startIndex)
                        .FindPropertyRelative("position").vector3Value);
                configure.Invoke(bootstrap, new object[] { party });

                Component controller = party.GetComponent("WorldMapPartyController");
                Component location = locations[startIndex].GetComponent("WorldMapLocation");
                System.Type dragStartHandler = FindType("CryingSnow.StackCraft.ICardDragStartHandler");
                Assert.That(dragStartHandler, Is.Not.Null);
                Assert.That(dragStartHandler.IsAssignableFrom(controller.GetType()), Is.True,
                    "人物卡拖拽开始时需要走统一回调解除人物槽挂载");

                dragStartHandler.GetMethod("HandleDragStarted")
                    .Invoke(controller, new object[] { party });

                Assert.That(party.transform.parent, Is.Null);
                Assert.That(
                    location.GetType().GetProperty("DockedParty").GetValue(location),
                    Is.Null,
                    "人物卡被拿起后地点人物槽必须立即清空");
                Assert.That(
                    controller.GetType().GetProperty("CurrentLocationIndex").GetValue(controller),
                    Is.EqualTo(startIndex),
                    "拿起人物卡只解除视觉驻扎，投放前仍需保留出发地点");
            }
            finally
            {
                DestroyTestCard(party);
                foreach (Component location in locations)
                    DestroyTestCard(location);
            }
        }

        [Test]
        public void WorldMapBootstrap_RestoresPersonSlotWhenPartyLoadsBeforeLocations()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component =>
                    component.GetType().FullName == "CryingSnow.StackCraft.WorldMapBootstrap");
            Assert.That(bootstrap, Is.Not.Null);

            var serialized = new SerializedObject(bootstrap);
            SerializedProperty locationSpawns = serialized.FindProperty("locationSpawns");
            SerializedProperty partyDefinition = serialized.FindProperty("partyDefinition");
            SerializedProperty initialLocation = serialized.FindProperty("initialPartyLocationIndex");
            MethodInfo configure = bootstrap.GetType().GetMethod("ConfigureSpawnedCard");
            int startIndex = initialLocation.intValue;
            Component party = CreateUninitializedCard(
                partyDefinition.objectReferenceValue,
                "Party Restored Before Locations");
            var locations = new List<Component>();
            try
            {
                SetTestCardStackPosition(
                    party,
                    locationSpawns.GetArrayElementAtIndex(startIndex)
                        .FindPropertyRelative("position").vector3Value);
                configure.Invoke(bootstrap, new object[] { party });

                for (int index = 0; index < locationSpawns.arraySize; index++)
                {
                    SerializedProperty spawn = locationSpawns.GetArrayElementAtIndex(index);
                    Component locationCard = CreateUninitializedCard(
                        spawn.FindPropertyRelative("definition").objectReferenceValue,
                        $"Late Location {index}");
                    SetTestCardStackPosition(
                        locationCard,
                        spawn.FindPropertyRelative("position").vector3Value);
                    configure.Invoke(bootstrap, new object[] { locationCard });
                    locations.Add(locationCard);
                }

                Component restoredLocation = locations[startIndex].GetComponent("WorldMapLocation");
                Assert.That(party.transform.parent, Is.SameAs(locations[startIndex].transform),
                    "无论存档卡牌恢复顺序如何，小队最终都必须重新进入地点人物槽");
                Assert.That(
                    restoredLocation.GetType().GetProperty("DockedParty").GetValue(restoredLocation),
                    Is.SameAs(party));
            }
            finally
            {
                DestroyTestCard(party);
                foreach (Component location in locations)
                    DestroyTestCard(location);
            }
        }

        [Test]
        public void WorldMapLocation_NonInstantAttachKeepsItsWorldPositionUntilTweenRuns()
        {
            System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
            Component locationCard = CreateUninitializedCard(null, "Animated Location");
            Component partyCard = CreateUninitializedCard(null, "Returning Party");
            Component location = locationCard.gameObject.AddComponent(locationType);
            try
            {
                locationType.GetMethod("Initialize").Invoke(location, new object[] { 0, locationCard });
                Vector3 droppedPosition = new(3f, 0f, -2f);
                SetTestCardStackPosition(partyCard, droppedPosition);

                locationType.GetMethod("AttachParty").Invoke(
                    location,
                    new object[] { partyCard, new Vector3(0f, 0.01f, -0.55f), 0.78f, false });

                MethodInfo update = locationCard.GetComponent("WorldMapPersonSlot").GetType().GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                update.Invoke(locationCard.GetComponent("WorldMapPersonSlot"), null);

                Assert.That(partyCard.transform.position, Is.EqualTo(droppedPosition),
                    "非即时驻扎应从投放位置播放回位动画，漂浮逻辑不能在 tween 前抢写位置");
            }
            finally
            {
                partyCard?.GetType().GetMethod("KillTweens")?.Invoke(partyCard, null);
                if (partyCard != null && partyCard.transform.parent != null)
                    partyCard.transform.SetParent(null, true);
                DestroyTestCard(locationCard);
                DestroyTestCard(partyCard);
            }
        }

        [Test]
        public void WorldMapLocation_ReplacesOccupantAndRestoresEachPartyOriginalScale()
        {
            System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
            Component locationCard = CreateUninitializedCard(null, "Single Party Slot");
            Component firstParty = CreateUninitializedCard(null, "First Party");
            Component secondParty = CreateUninitializedCard(null, "Second Party");
            Component location = locationCard.gameObject.AddComponent(locationType);
            Vector3 firstScale = new(1.2f, 1.2f, 1.2f);
            Vector3 secondScale = new(0.65f, 0.65f, 0.65f);
            firstParty.transform.localScale = firstScale;
            secondParty.transform.localScale = secondScale;
            try
            {
                locationType.GetMethod("Initialize").Invoke(location, new object[] { 0, locationCard });
                MethodInfo attach = locationType.GetMethod("AttachParty");
                MethodInfo detach = locationType.GetMethod("DetachParty");
                Vector3 dock = new(0f, 0.01f, -0.55f);

                attach.Invoke(location, new object[] { firstParty, dock, 0.78f, true });
                attach.Invoke(location, new object[] { secondParty, dock, 0.78f, true });

                Assert.That(firstParty.transform.parent, Is.Null,
                    "单一地点槽被新小队占用时必须完整释放旧小队");
                Assert.That(firstParty.transform.localScale, Is.EqualTo(firstScale));

                detach.Invoke(location, new object[] { secondParty });
                Assert.That(secondParty.transform.localScale, Is.EqualTo(secondScale),
                    "离开地点后必须恢复挂载前比例，而不是固定写回 1");
            }
            finally
            {
                if (firstParty != null && firstParty.transform.parent != null)
                    firstParty.transform.SetParent(null, true);
                if (secondParty != null && secondParty.transform.parent != null)
                    secondParty.transform.SetParent(null, true);
                DestroyTestCard(locationCard);
                DestroyTestCard(firstParty);
                DestroyTestCard(secondParty);
            }
        }

        [Test]
        public void DestroyingWorldMapLocationReleasesPartyInsteadOfDestroyingIt()
        {
            System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
            Component locationCard = CreateUninitializedCard(null, "Disposable Location");
            Component partyCard = CreateUninitializedCard(null, "Surviving Party");
            Object locationSettings = (Object)locationCard.GetType().GetProperty("Settings").GetValue(locationCard);
            Object partySettings = (Object)partyCard.GetType().GetProperty("Settings").GetValue(partyCard);
            Component location = locationCard.gameObject.AddComponent(locationType);
            try
            {
                locationType.GetMethod("Initialize").Invoke(location, new object[] { 0, locationCard });
                locationType.GetMethod("AttachParty").Invoke(
                    location,
                    new object[] { partyCard, new Vector3(0f, 0.01f, -0.55f), 0.78f, true });

                object locationStack = locationCard.GetType().GetProperty("Stack").GetValue(locationCard);
                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode!"));
                locationStack.GetType().GetMethod("DestroyCard")
                    .Invoke(locationStack, new object[] { locationCard });

                Assert.That(partyCard == null, Is.False,
                    "地点卡销毁前必须先释放小队，不能把独立小队栈作为子对象一起删除");
                Assert.That(partyCard.transform.parent, Is.Null);
                Assert.That(partyCard.GetType().GetProperty("Stack").GetValue(partyCard), Is.Not.Null);
            }
            finally
            {
                if (locationCard != null)
                    Object.DestroyImmediate(locationCard.gameObject);
                if (partyCard != null)
                    Object.DestroyImmediate(partyCard.gameObject);
                if (locationSettings != null)
                    Object.DestroyImmediate(locationSettings);
                if (partySettings != null)
                    Object.DestroyImmediate(partySettings);
            }
        }

        [Test]
        public void CardPhysicsSolver_DoesNotSeparatePartyDockedToItsLocation()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour board = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().FullName == "CryingSnow.StackCraft.Board");
            board.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(board, null);

            System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
            System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            System.Type solverType = FindType("CryingSnow.StackCraft.CardPhysicsSolver");
            Component locationCard = CreateUninitializedCard(null, "Solver Location");
            Component partyCard = CreateUninitializedCard(null, "Solver Party");
            Component nearbyCard = CreateUninitializedCard(null, "Nearby Stack");
            Component location = locationCard.gameObject.AddComponent(locationType);
            try
            {
                locationCard.GetType().GetProperty("Size").SetValue(locationCard, Vector2.one);
                partyCard.GetType().GetProperty("Size").SetValue(partyCard, Vector2.one);
                nearbyCard.GetType().GetProperty("Size").SetValue(nearbyCard, Vector2.one);
                locationType.GetMethod("Initialize").Invoke(location, new object[] { 0, locationCard });
                locationType.GetMethod("AttachParty").Invoke(
                    location,
                    new object[] { partyCard, new Vector3(0f, 0.01f, -0.2f), 0.78f, true });
                SetTestCardStackPosition(nearbyCard, new Vector3(0f, 0f, -0.2f));

                object locationStack = locationCard.GetType().GetProperty("Stack").GetValue(locationCard);
                object partyStack = partyCard.GetType().GetProperty("Stack").GetValue(partyCard);
                object nearbyStack = nearbyCard.GetType().GetProperty("Stack").GetValue(nearbyCard);
                Vector3 dockPosition = (Vector3)stackType.GetProperty("TargetPosition").GetValue(partyStack);
                System.Type listType = typeof(List<>).MakeGenericType(stackType);
                var stacks = (IList)System.Activator.CreateInstance(listType);
                stacks.Add(locationStack);
                stacks.Add(partyStack);
                stacks.Add(nearbyStack);

                MethodInfo resolveWorldOverlaps = solverType.GetMethods(
                        BindingFlags.Public | BindingFlags.Static)
                    .Single(method =>
                        method.Name == "ResolveOverlaps" &&
                        method.GetParameters().Length == 3);
                resolveWorldOverlaps.Invoke(
                    null,
                    new object[] { stacks, null, 8 });

                Vector3 resolvedPosition = (Vector3)stackType.GetProperty("TargetPosition").GetValue(partyStack);
                Assert.That(resolvedPosition, Is.EqualTo(dockPosition),
                    "全局重叠求解不能把已挂载的小队从地点槽推开");
            }
            finally
            {
                if (partyCard != null && partyCard.transform.parent != null)
                    partyCard.transform.SetParent(null, true);
                DestroyTestCard(locationCard);
                DestroyTestCard(partyCard);
                DestroyTestCard(nearbyCard);
            }
        }

        [Test]
        public void CardManager_ExplicitInteractionRectParticipatesInOverlapResolution()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Location.unity", OpenSceneMode.Single);
            MonoBehaviour board = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName == "CryingSnow.StackCraft.Board");
            board.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(board, null);
            MonoBehaviour cardManager = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName == "CryingSnow.StackCraft.CardManager");
            cardManager.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cardManager, null);

            Object playerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Component card = CreateUninitializedCard(playerDefinition, "Interaction Rect Overlap Card");
            var rectObject = new GameObject("Explicit Interaction Rect", typeof(RectTransform));
            Component interactionRect = rectObject.AddComponent(
                FindType("CryingSnow.StackCraft.CombatRect"));
            try
            {
                card.GetType().GetProperty("Size").SetValue(card, Vector2.one);
                SetTestCardStackPosition(card, new Vector3(0.1f, 0f, 0.1f));
                object stack = card.GetType().GetProperty("Stack").GetValue(card);
                cardManager.GetType().GetMethod("RegisterStack").Invoke(cardManager, new[] { stack });

                RectTransform rectTransform = rectObject.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(2f, 2f);
                interactionRect.GetType().GetProperty("Rect").SetValue(interactionRect, rectTransform);

                MethodInfo resolveExplicit = cardManager.GetType().GetMethod(
                    "ResolveOverlaps",
                    new[] { interactionRect.GetType(), stack.GetType() });
                resolveExplicit.Invoke(cardManager, new[] { interactionRect, null });

                Vector3 resolved = (Vector3)stack.GetType()
                    .GetProperty("TargetPosition").GetValue(stack);
                Assert.That(resolved, Is.Not.EqualTo(new Vector3(0.1f, 0f, 0.1f)),
                    "显式传入的对话/战斗框必须参与卡堆避让，不能只读取活动战斗列表");
            }
            finally
            {
                DestroyTestCard(card);
                Object.DestroyImmediate(rectObject);
            }
        }

        [Test]
        public void CardManager_ExplicitInteractionRectHonorsIgnoredStack()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Location.unity", OpenSceneMode.Single);
            MonoBehaviour board = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName == "CryingSnow.StackCraft.Board");
            board.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(board, null);
            MonoBehaviour cardManager = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName == "CryingSnow.StackCraft.CardManager");
            cardManager.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cardManager, null);

            Object playerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Component card = CreateUninitializedCard(playerDefinition, "Ignored Interaction Stack");
            var rectObject = new GameObject("Explicit Interaction Rect", typeof(RectTransform));
            Component interactionRect = rectObject.AddComponent(
                FindType("CryingSnow.StackCraft.CombatRect"));
            try
            {
                card.GetType().GetProperty("Size").SetValue(card, Vector2.one);
                Vector3 startPosition = new Vector3(0.1f, 0f, 0.1f);
                SetTestCardStackPosition(card, startPosition);
                object stack = card.GetType().GetProperty("Stack").GetValue(card);
                cardManager.GetType().GetMethod("RegisterStack").Invoke(cardManager, new[] { stack });

                RectTransform rectTransform = rectObject.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(2f, 2f);
                interactionRect.GetType().GetProperty("Rect").SetValue(interactionRect, rectTransform);

                MethodInfo resolveExplicit = cardManager.GetType().GetMethod(
                    "ResolveOverlaps",
                    new[] { interactionRect.GetType(), stack.GetType() });
                resolveExplicit.Invoke(cardManager, new[] { interactionRect, stack });

                Vector3 resolved = (Vector3)stack.GetType()
                    .GetProperty("TargetPosition").GetValue(stack);
                Assert.That(resolved, Is.EqualTo(startPosition),
                    "加入战斗时明确忽略的来源卡堆不能被互动框推开");
            }
            finally
            {
                DestroyTestCard(card);
                Object.DestroyImmediate(rectObject);
            }
        }

        [Test]
        public void CombatManager_InteractionRectPreservesMutableCombatLists()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Location.unity", OpenSceneMode.Single);
            foreach (string typeName in new[]
            {
                "CryingSnow.StackCraft.Board",
                "CryingSnow.StackCraft.CardManager"
            })
            {
                MonoBehaviour singleton = Object.FindObjectsOfType<MonoBehaviour>(true)
                    .First(component => component.GetType().FullName == typeName);
                singleton.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(singleton, null);
            }
            MonoBehaviour combatManager = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName == "CryingSnow.StackCraft.CombatManager");
            combatManager.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(combatManager, null);

            Object playerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Component firstCard = CreateUninitializedCard(playerDefinition, "Combat List First");
            Component secondCard = CreateUninitializedCard(playerDefinition, "Combat List Second");
            Component interactionRect = null;
            try
            {
                firstCard.GetType().GetProperty("Size").SetValue(firstCard, Vector2.one);
                secondCard.GetType().GetProperty("Size").SetValue(secondCard, Vector2.one);
                System.Type cardType = firstCard.GetType();
                System.Type listType = typeof(List<>).MakeGenericType(cardType);
                var firstSide = (IList)System.Activator.CreateInstance(listType);
                var secondSide = (IList)System.Activator.CreateInstance(listType);
                firstSide.Add(firstCard);
                secondSide.Add(secondCard);

                MethodInfo listOverload = combatManager.GetType().GetMethod(
                    "CreateInteractionRect",
                    new[] { listType, listType });
                Assert.That(listOverload, Is.Not.Null,
                    "战斗创建必须有保留可变列表引用的重载，不能把双方复制成快照");
                interactionRect = (Component)listOverload.Invoke(
                    combatManager,
                    new object[] { firstSide, secondSide });
                Assert.That(interactionRect, Is.Not.Null);

                FieldInfo attackers = interactionRect.GetType().GetField(
                    "_attackers",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo defenders = interactionRect.GetType().GetField(
                    "_defenders",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(attackers.GetValue(interactionRect), Is.SameAs(firstSide));
                Assert.That(defenders.GetValue(interactionRect), Is.SameAs(secondSide));
            }
            finally
            {
                DestroyTestCard(firstCard);
                DestroyTestCard(secondCard);
                if (interactionRect != null)
                    Object.DestroyImmediate(interactionRect.gameObject);
            }
        }

        [Test]
        public void WorldMapBootstrap_DocksConfiguredPartyIntoTheCurrentLocationSlot()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component =>
                    component.GetType().FullName == "CryingSnow.StackCraft.WorldMapBootstrap");
            Assert.That(bootstrap, Is.Not.Null);

            var serialized = new SerializedObject(bootstrap);
            SerializedProperty locationSpawns = serialized.FindProperty("locationSpawns");
            SerializedProperty partyDefinition = serialized.FindProperty("partyDefinition");
            SerializedProperty initialLocation = serialized.FindProperty("initialPartyLocationIndex");
            SerializedProperty dockOffset = serialized.FindProperty("partyDockOffset");
            SerializedProperty dockScale = serialized.FindProperty("partyDockScale");
            Assert.That(locationSpawns, Is.Not.Null);
            Assert.That(partyDefinition.objectReferenceValue, Is.Not.Null);
            Assert.That(dockScale, Is.Not.Null);
            Assert.That(dockScale.floatValue, Is.EqualTo(0.78f).Within(0.001f));

            MethodInfo configure = bootstrap.GetType().GetMethod("ConfigureSpawnedCard");
            var locations = new List<Component>();
            Component party = null;
            try
            {
                for (int index = 0; index < locationSpawns.arraySize; index++)
                {
                    SerializedProperty spawn = locationSpawns.GetArrayElementAtIndex(index);
                    Component locationCard = CreateUninitializedCard(
                        spawn.FindPropertyRelative("definition").objectReferenceValue,
                        $"Dock Location {index}");
                    SetTestCardStackPosition(
                        locationCard,
                        spawn.FindPropertyRelative("position").vector3Value);
                    configure.Invoke(bootstrap, new object[] { locationCard });
                    locations.Add(locationCard);
                }

                int startIndex = initialLocation.intValue;
                party = CreateUninitializedCard(partyDefinition.objectReferenceValue, "Docked Party");
                Vector3 startPosition = locationSpawns.GetArrayElementAtIndex(startIndex)
                    .FindPropertyRelative("position").vector3Value + dockOffset.vector3Value;
                SetTestCardStackPosition(party, startPosition);
                configure.Invoke(bootstrap, new object[] { party });

                Component location = locations[startIndex].GetComponent("WorldMapLocation");
                PropertyInfo dockedParty = location.GetType().GetProperty("DockedParty");
                Assert.That(party.transform.parent, Is.SameAs(locations[startIndex].transform),
                    "驻扎后小队需要像装备卡一样成为地点卡的视觉子对象");
                Assert.That(dockedParty.GetValue(location), Is.SameAs(party));
                Assert.That(party.transform.localPosition, Is.EqualTo(dockOffset.vector3Value));
                Assert.That(party.transform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(party.transform.localScale, Is.EqualTo(Vector3.one * dockScale.floatValue),
                    "挂载状态需要按场景配置缩小小队卡，让地点卡保持视觉主体");
            }
            finally
            {
                DestroyTestCard(party);
                foreach (Component location in locations)
                    DestroyTestCard(location);
            }
        }

        [Test]
        public void WorldMapBootstrap_TravelUsesOneSecondAndOriginalProgressBarPrefab()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component =>
                    component.GetType().FullName == "CryingSnow.StackCraft.WorldMapBootstrap");
            Assert.That(bootstrap, Is.Not.Null);

            var serialized = new SerializedObject(bootstrap);
            SerializedProperty duration = serialized.FindProperty("partyTravelDuration");
            SerializedProperty progressPrefab = serialized.FindProperty("travelProgressUIPrefab");
            Assert.That(duration, Is.Not.Null, "世界地图需要独立的地点间旅行时长配置");
            Assert.That(duration.floatValue, Is.EqualTo(1f).Within(0.001f));
            Assert.That(progressPrefab, Is.Not.Null);
            Assert.That(progressPrefab.objectReferenceValue, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(progressPrefab.objectReferenceValue),
                Is.EqualTo("Assets/StackCraft/Prefabs/UI/ProgressUI.prefab"),
                "旅行必须复用原项目堆叠制作使用的进度条 prefab");
        }

        [Test]
        public void WorldMapLocation_TravelHighlightIsGreenAndUsesSelectedLift()
        {
            System.Type locationType = FindType("CryingSnow.StackCraft.WorldMapLocation");
            Component locationCard = CreateUninitializedCard(null, "Travel Highlight Location");
            Component location = locationCard.gameObject.AddComponent(locationType);
            try
            {
                locationType.GetMethod("Initialize").Invoke(location, new object[] { 0, locationCard });
                MethodInfo setTravelHighlighted = locationType.GetMethod(
                    "SetTravelHighlighted",
                    new[] { typeof(bool), typeof(bool) });
                PropertyInfo isTravelHighlighted = locationType.GetProperty("IsTravelHighlighted");
                Assert.That(setTravelHighlighted, Is.Not.Null);
                Assert.That(isTravelHighlighted, Is.Not.Null);

                Vector3 restingPosition = locationCard.transform.localPosition;
                setTravelHighlighted.Invoke(location, new object[] { true, true });

                Assert.That(isTravelHighlighted.GetValue(location), Is.True);
                Assert.That(locationCard.transform.localPosition.y,
                    Is.GreaterThan(restingPosition.y + 0.01f));
                Transform highlight = locationCard.transform.Find("Highlight");
                Assert.That(highlight, Is.Not.Null);
                Assert.That(highlight.gameObject.activeSelf, Is.True);
                var propertyBlock = new MaterialPropertyBlock();
                highlight.GetComponent<Renderer>().GetPropertyBlock(propertyBlock);
                Color color = propertyBlock.GetColor(Shader.PropertyToID("_OutlineColor"));
                Assert.That(color.g, Is.GreaterThan(color.r));
                Assert.That(color.g, Is.GreaterThan(color.b), "旅行中的地点必须使用绿色流动外框");

                MethodInfo setSelected = locationType.GetMethod(
                    "SetSelected",
                    new[] { typeof(bool), typeof(bool) });
                setSelected.Invoke(location, new object[] { true, true });
                setSelected.Invoke(location, new object[] { false, true });
                Assert.That(locationCard.transform.localPosition.y,
                    Is.GreaterThan(restingPosition.y + 0.01f),
                    "普通选中状态切换不能取消旅行地点的抬升和浮动效果");

                setTravelHighlighted.Invoke(location, new object[] { false, true });
                Assert.That(isTravelHighlighted.GetValue(location), Is.False);
                Assert.That(locationCard.transform.localPosition, Is.EqualTo(restingPosition));
            }
            finally
            {
                DestroyTestCard(locationCard);
            }
        }

        [Test]
        public void WorldMapPartyController_DropStacksForOneSecondThenDocksAtDestination()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component =>
                    component.GetType().FullName == "CryingSnow.StackCraft.WorldMapBootstrap");
            var serialized = new SerializedObject(bootstrap);
            SerializedProperty locationSpawns = serialized.FindProperty("locationSpawns");
            SerializedProperty partyDefinition = serialized.FindProperty("partyDefinition");
            SerializedProperty initialLocation = serialized.FindProperty("initialPartyLocationIndex");
            MethodInfo configure = bootstrap.GetType().GetMethod("ConfigureSpawnedCard");
            var locations = new List<Component>();
            Component party = null;
            try
            {
                for (int index = 0; index < locationSpawns.arraySize; index++)
                {
                    SerializedProperty spawn = locationSpawns.GetArrayElementAtIndex(index);
                    Component locationCard = CreateUninitializedCard(
                        spawn.FindPropertyRelative("definition").objectReferenceValue,
                        $"Travel Location {index}");
                    SetTestCardStackPosition(
                        locationCard,
                        spawn.FindPropertyRelative("position").vector3Value);
                    configure.Invoke(bootstrap, new object[] { locationCard });
                    locations.Add(locationCard);
                }

                int originIndex = initialLocation.intValue;
                const int destinationIndex = 1;
                party = CreateUninitializedCard(partyDefinition.objectReferenceValue, "Traveling Party");
                SetTestCardStackPosition(
                    party,
                    locationSpawns.GetArrayElementAtIndex(originIndex)
                        .FindPropertyRelative("position").vector3Value);
                configure.Invoke(bootstrap, new object[] { party });

                Component controller = party.GetComponent("WorldMapPartyController");
                controller.GetType().GetMethod("HandleDragStarted")
                    .Invoke(controller, new object[] { party });
                Vector3 destinationPosition = locationSpawns.GetArrayElementAtIndex(destinationIndex)
                    .FindPropertyRelative("position").vector3Value;
                bool handled = (bool)controller.GetType().GetMethod("HandleDrop")
                    .Invoke(controller, new object[] { party, destinationPosition });

                Assert.That(handled, Is.True);
                Assert.That(controller.GetType().GetProperty("IsTraveling").GetValue(controller), Is.True);
                Assert.That(
                    controller.GetType().GetProperty("CurrentLocationIndex").GetValue(controller),
                    Is.EqualTo(originIndex),
                    "进度条完成前小队仍属于起始地点");
                Assert.That(
                    locations[originIndex].GetComponent("WorldMapLocation").GetType()
                        .GetProperty("IsTravelHighlighted").GetValue(
                            locations[originIndex].GetComponent("WorldMapLocation")),
                    Is.True);
                Assert.That(
                    locations[destinationIndex].GetComponent("WorldMapLocation").GetType()
                        .GetProperty("IsTravelHighlighted").GetValue(
                            locations[destinationIndex].GetComponent("WorldMapLocation")),
                    Is.True);

                MethodInfo getTravelStackPosition = bootstrap.GetType().GetMethod("GetTravelStackPosition");
                Assert.That(getTravelStackPosition, Is.Not.Null);
                Vector3 expectedStackPosition = (Vector3)getTravelStackPosition.Invoke(
                    bootstrap,
                    new object[] { destinationIndex, party });
                object partyStack = party.GetType().GetProperty("Stack").GetValue(party);
                Assert.That(
                    partyStack.GetType().GetProperty("TargetPosition").GetValue(partyStack),
                    Is.EqualTo(expectedStackPosition),
                    "等待期间小队卡必须使用原生 StackStep 堆在目标地点卡上");
                Assert.That(controller.GetType().GetProperty("TravelProgressUI").GetValue(controller), Is.Not.Null);

                MethodInfo tickTravel = controller.GetType().GetMethod(
                    "TickTravel",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(tickTravel, Is.Not.Null);
                tickTravel.Invoke(controller, new object[] { 0.5f });
                Assert.That(
                    (float)controller.GetType().GetProperty("TravelProgress").GetValue(controller),
                    Is.EqualTo(0.5f).Within(0.01f));
                tickTravel.Invoke(controller, new object[] { 0.5f });

                Assert.That(controller.GetType().GetProperty("IsTraveling").GetValue(controller), Is.False);
                Assert.That(
                    controller.GetType().GetProperty("CurrentLocationIndex").GetValue(controller),
                    Is.EqualTo(destinationIndex));
                Component destinationLocation = locations[destinationIndex].GetComponent("WorldMapLocation");
                Assert.That(
                    destinationLocation.GetType().GetProperty("DockedParty").GetValue(destinationLocation),
                    Is.SameAs(party),
                    "进度完成后小队才真正进入目标地点人物槽");
                Assert.That(controller.GetType().GetProperty("TravelProgressUI").GetValue(controller), Is.Null);

                controller.GetType().GetMethod("HandleDragStarted")
                    .Invoke(controller, new object[] { party });
                Vector3 returnPosition = locationSpawns.GetArrayElementAtIndex(originIndex)
                    .FindPropertyRelative("position").vector3Value;
                controller.GetType().GetMethod("HandleDrop")
                    .Invoke(controller, new object[] { party, returnPosition });
                Assert.That(controller.GetType().GetProperty("IsTraveling").GetValue(controller), Is.True);

                controller.GetType().GetMethod(
                    "OnDisable",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(controller, null);

                Assert.That(controller.GetType().GetProperty("IsTraveling").GetValue(controller), Is.False,
                    "禁用小队控制器必须取消尚未完成的旅行");
                Assert.That(
                    partyStack.GetType().GetProperty("IsLocked").GetValue(partyStack),
                    Is.False,
                    "取消旅行后小队栈必须解锁");
                Assert.That(controller.GetType().GetProperty("TravelProgressUI").GetValue(controller), Is.Null);
                Assert.That(
                    locations[originIndex].GetComponent("WorldMapLocation").GetType()
                        .GetProperty("IsTravelHighlighted").GetValue(
                            locations[originIndex].GetComponent("WorldMapLocation")),
                    Is.False);
                Assert.That(
                    locations[destinationIndex].GetComponent("WorldMapLocation").GetType()
                        .GetProperty("IsTravelHighlighted").GetValue(
                            locations[destinationIndex].GetComponent("WorldMapLocation")),
                    Is.False);
                Assert.That(
                    destinationLocation.GetType().GetProperty("DockedParty").GetValue(destinationLocation),
                    Is.SameAs(party),
                    "取消旅行时小队应回到本次旅行的起始地点人物槽");
            }
            finally
            {
                DestroyTestCard(party);
                foreach (Component location in locations)
                    DestroyTestCard(location);
            }
        }

        [Test]
        public void WorldMapBootstrap_DoesNotOverlayRuntimeLinesOnTheBakedRouteMap()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().FullName == "CryingSnow.StackCraft.WorldMapBootstrap");
            Assert.That(bootstrap, Is.Not.Null);

            int lineCountBeforeInitialization = Object.FindObjectsOfType<LineRenderer>(true).Length;
            MethodInfo start = bootstrap.GetType().GetMethod(
                "Start",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(start, Is.Not.Null);
            start.Invoke(bootstrap, null);

            Assert.That(
                Object.FindObjectsOfType<LineRenderer>(true).Length,
                Is.EqualTo(lineCountBeforeInitialization),
                "路线已绘制在地图底图中，世界地图初始化不能再叠加 LineRenderer");
        }

        private static Component CreateUninitializedCard(Object definition, string name)
        {
            System.Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
            System.Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            System.Type settingsType = FindType("CryingSnow.StackCraft.CardSettings");
            var gameObject = new GameObject(name);
            gameObject.AddComponent<MeshFilter>();
            Component card = gameObject.AddComponent(cardType);
            cardType.GetField("_renderer", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(card, gameObject.GetComponent<MeshRenderer>());
            cardType.GetField("_col", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(card, gameObject.GetComponent<BoxCollider>());
            cardType.GetProperty("Definition").SetValue(card, definition);
            ScriptableObject settings = ScriptableObject.CreateInstance(settingsType);
            cardType.GetProperty("Settings").SetValue(card, settings);
            _ = System.Activator.CreateInstance(stackType, card, Vector3.zero);
            return card;
        }

        private static void DestroyTestCard(Component card)
        {
            if (card == null)
                return;

            Object settings = (Object)card.GetType().GetProperty("Settings").GetValue(card);
            Object.DestroyImmediate(card.gameObject);
            if (settings != null)
                Object.DestroyImmediate(settings);
        }

        private static void SetTestCardStackPosition(Component card, Vector3 position)
        {
            object stack = card.GetType().GetProperty("Stack").GetValue(card);
            stack.GetType().GetMethod("SetTargetPosition")
                .Invoke(stack, new object[] { position, true });
        }

        private static System.Type FindType(string fullName)
        {
            return System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(type => type != null);
        }

        [Test]
        public void MainScene_KeepsOriginalTimeManagerForTheOriginalUi()
        {
            EditorSceneManager.OpenScene("Assets/StackCraft/Scenes/Main.unity", OpenSceneMode.Single);
            MonoBehaviour legacyClock = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().FullName == "CryingSnow.StackCraft.TimeManager");
            WorldClockDriver driver = Object.FindObjectOfType<WorldClockDriver>(true);

            Assert.That(legacyClock, Is.Not.Null);
            Assert.That(legacyClock.enabled, Is.True,
                "原项目 UIRoot 依赖原生 TimeManager，主场景不能禁用它");
            Assert.That(driver, Is.Null,
                "Main 场景不应再次挂载属于重复 GameUiRoot 的时钟驱动");
        }

        [Test]
        public void WorldClockDriver_SpeedControlsDriveNativeSimulationTimeScale()
        {
            var gameObject = new GameObject("Native Time Scale Driver");
            try
            {
                var driver = gameObject.AddComponent<WorldClockDriver>();
                driver.Initialize(new PlayableLoopSession(1f, 360d, 8, 100f));

                driver.SetFastSpeed();
                Assert.That(Time.timeScale, Is.EqualTo(4f));

                driver.SetPaused(true);
                Assert.That(Time.timeScale, Is.Zero);

                driver.SetPaused(false);
                Assert.That(Time.timeScale, Is.EqualTo(4f));

                driver.SetNormalSpeed();
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally
            {
                Time.timeScale = 1f;
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void GameUiPresenter_RebindsSpeedButtonsToCurrentDriver()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CardColony/Prefabs/GameUiRoot.prefab");
            GameObject ui = Object.Instantiate(prefab);
            var firstObject = new GameObject("First Driver");
            var secondObject = new GameObject("Second Driver");
            try
            {
                var presenter = ui.GetComponentInChildren<GameUiPresenter>(true);
                var first = firstObject.AddComponent<WorldClockDriver>();
                var second = secondObject.AddComponent<WorldClockDriver>();
                first.Initialize(new PlayableLoopSession(1f, 0d, 8, 100f));
                second.Initialize(new PlayableLoopSession(1f, 0d, 8, 100f));

                presenter.Bind(first);
                presenter.Bind(second);
                var fastButton = (UnityEngine.UI.Button)typeof(GameUiPresenter)
                    .GetField("fastSpeedButton", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(presenter);
                fastButton.onClick.Invoke();

                Assert.That(second.Session.Clock.Speed, Is.EqualTo(WorldClockSpeed.Fast));
                Assert.That(first.Session.Clock.Speed, Is.EqualTo(WorldClockSpeed.Normal));
            }
            finally
            {
                Object.DestroyImmediate(secondObject);
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(ui);
            }
        }

        [Test]
        public void GameUiPresenter_CanBindBeforeWorldClockSessionIsInitialized()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CardColony/Prefabs/GameUiRoot.prefab");
            GameObject ui = Object.Instantiate(prefab);
            var driverObject = new GameObject("Deferred Driver");
            try
            {
                var presenter = ui.GetComponentInChildren<GameUiPresenter>(true);
                var driver = driverObject.AddComponent<WorldClockDriver>();
                typeof(WorldClockDriver)
                    .GetField("<Session>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(driver, null);

                Assert.DoesNotThrow(() => presenter.Bind(driver));

                driver.Initialize(new PlayableLoopSession(1f, 360d, 8, 100f));
                Assert.That(presenter.TimeText.text, Does.Contain("1"));
            }
            finally
            {
                Object.DestroyImmediate(driverObject);
                Object.DestroyImmediate(ui);
            }
        }

        [Test]
        public void RiverbendInnInterior_UsesGeneratedBackgroundAndFiveCardArts()
        {
            string[] requiredTextures =
            {
                "Assets/CardColony/Art/Backgrounds/Inn/RiverbendInnInteriorBackground.png",
                "Assets/CardColony/Art/CardArts/Inn/Innkeeper.png",
                "Assets/CardColony/Art/CardArts/Inn/Waiter.png",
                "Assets/CardColony/Art/CardArts/Inn/Reception.png",
                "Assets/CardColony/Art/CardArts/Inn/Table.png",
                "Assets/CardColony/Art/CardArts/Inn/Bed.png"
            };

            foreach (string path in requiredTextures)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture, Is.Not.Null, $"旅馆视觉资源尚未导入：{path}");
                Assert.That(texture.width, Is.GreaterThanOrEqualTo(1024));
                Assert.That(texture.height, Is.GreaterThanOrEqualTo(900));
            }
        }

        [Test]
        public void RiverbendInnBackground_PreservesSixteenByNineImportAspect()
        {
            const string path =
                "Assets/CardColony/Art/Backgrounds/Inn/RiverbendInnInteriorBackground.png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            Assert.That(texture, Is.Not.Null);
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None),
                "旅馆底图不能被 Unity 强制缩放成 2:1");
            Assert.That((float)texture.width / texture.height, Is.EqualTo(16f / 9f).Within(0.01f));
        }

        [Test]
        public void RiverbendInnLocation_ConfiguresReceptionStaffTablesAndFourBeds()
        {
            Object definition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_RiverbendInn.asset");
            Assert.That(definition, Is.Not.Null, "旅馆内部必须拥有独立 LocationDefinition");

            var serialized = new SerializedObject(definition);
            Assert.That(serialized.FindProperty("id").stringValue, Is.EqualTo("riverbend-inn"));
            Assert.That(serialized.FindProperty("displayName").stringValue, Is.EqualTo("河湾旅馆"));
            Assert.That(serialized.FindProperty("backgroundTexture").objectReferenceValue, Is.Not.Null);

            SerializedProperty spawns = serialized.FindProperty("initialCardSpawns");
            Assert.That(spawns, Is.Not.Null);
            Assert.That(spawns.arraySize, Is.EqualTo(10),
                "内部应为前台、老板、三张桌子、小二和四张床，共十张固定内容卡");

            var ids = new List<string>();
            for (int index = 0; index < spawns.arraySize; index++)
            {
                Object card = spawns.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("definition").objectReferenceValue;
                Assert.That(card, Is.Not.Null);
                ids.Add(new SerializedObject(card).FindProperty("id").stringValue);
            }

            Assert.That(ids.Count(id => id == "riverbend-inn-reception"), Is.EqualTo(1));
            Assert.That(ids.Count(id => id == "riverbend-innkeeper"), Is.EqualTo(1));
            Assert.That(ids.Count(id => id == "riverbend-inn-table"), Is.EqualTo(3));
            Assert.That(ids.Count(id => id == "riverbend-inn-waiter"), Is.EqualTo(1));
            Assert.That(ids.Count(id => id == "riverbend-inn-bed"), Is.EqualTo(4));
        }

        [Test]
        public void RiverbendLocation_MapsInnBuildingCardToInnInterior()
        {
            Object riverbend = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset");
            Assert.That(riverbend, Is.Not.Null);

            SerializedProperty entrances = new SerializedObject(riverbend)
                .FindProperty("entrances");
            Assert.That(entrances, Is.Not.Null, "LocationDefinition 需要声明建筑入口");
            Assert.That(entrances.arraySize, Is.EqualTo(1));

            SerializedProperty entrance = entrances.GetArrayElementAtIndex(0);
            Object sourceCard = entrance.FindPropertyRelative("sourceCardDefinition")
                .objectReferenceValue;
            Assert.That(sourceCard, Is.Not.Null);
            Assert.That(
                new SerializedObject(sourceCard).FindProperty("id").stringValue,
                Is.EqualTo("riverbend-inn"));
            Assert.That(
                entrance.FindPropertyRelative("destinationLocationId").stringValue,
                Is.EqualTo("riverbend-inn"));
        }

        [Test]
        public void GameData_LocationHistoryUsesLastEnteredLocationAsReturnTarget()
        {
            System.Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            Assert.That(gameDataType, Is.Not.Null);
            object gameData = System.Activator.CreateInstance(gameDataType);

            MethodInfo push = gameDataType.GetMethod("PushLocation");
            MethodInfo tryPop = gameDataType.GetMethod("TryPopLocation");
            Assert.That(push, Is.Not.Null);
            Assert.That(tryPop, Is.Not.Null);

            push.Invoke(gameData, new object[] { "riverbend" });
            push.Invoke(gameData, new object[] { "riverbend-inn" });

            object[] firstPop = { null };
            Assert.That(tryPop.Invoke(gameData, firstPop), Is.True);
            Assert.That(firstPop[0], Is.EqualTo("riverbend-inn"));

            object[] secondPop = { null };
            Assert.That(tryPop.Invoke(gameData, secondPop), Is.True);
            Assert.That(secondPop[0], Is.EqualTo("riverbend"));

            object[] emptyPop = { null };
            Assert.That(tryPop.Invoke(gameData, emptyPop), Is.False);
            Assert.That(emptyPop[0], Is.Null);
        }

        [Test]
        public void GameData_PendingLocationPartyTransferIsConsumedOnce()
        {
            System.Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            object gameData = System.Activator.CreateInstance(gameDataType);
            MethodInfo mark = gameDataType.GetMethod("MarkLocationPartyTransferPending");
            MethodInfo consume = gameDataType.GetMethod("ConsumeLocationPartyTransferPending");
            Assert.That(mark, Is.Not.Null);
            Assert.That(consume, Is.Not.Null);

            mark.Invoke(gameData, null);
            Assert.That(consume.Invoke(gameData, null), Is.True);
            Assert.That(consume.Invoke(gameData, null), Is.False);
        }

        [Test]
        public void GameData_LocationTransitionReasonIsConsumedOnce()
        {
            System.Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            System.Type reasonType = FindType(
                "CryingSnow.StackCraft.LocationTransitionReason");
            Assert.That(reasonType, Is.Not.Null);
            object gameData = System.Activator.CreateInstance(gameDataType);
            MethodInfo mark = gameDataType.GetMethod("MarkLocationTransitionPending");
            MethodInfo consume = gameDataType.GetMethod("ConsumeLocationTransitionReason");
            Assert.That(mark, Is.Not.Null);
            Assert.That(consume, Is.Not.Null);

            object worldMapEntry = System.Enum.Parse(reasonType, "WorldMapEntry");
            object none = System.Enum.Parse(reasonType, "None");
            mark.Invoke(gameData, new[] { worldMapEntry });
            Assert.That(consume.Invoke(gameData, null), Is.EqualTo(worldMapEntry));
            Assert.That(consume.Invoke(gameData, null), Is.EqualTo(none));
        }

        [Test]
        public void LocationSceneController_ReplacesRestoredParentPartyWithoutRollingBackStats()
        {
            EditorSceneManager.OpenScene(
                "Assets/StackCraft/Scenes/Location.unity",
                OpenSceneMode.Single);
            System.Type gameDirectorType = FindType("CryingSnow.StackCraft.GameDirector");
            MonoBehaviour gameDirector = (MonoBehaviour)new GameObject("Test GameDirector")
                .AddComponent(gameDirectorType);
            gameDirectorType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                .SetValue(null, gameDirector);
            System.Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            object gameData = System.Activator.CreateInstance(gameDataType);
            gameDataType.GetField("GameplayPrefs").SetValue(
                gameData,
                System.Activator.CreateInstance(FindType("CryingSnow.StackCraft.GameplayPrefs")));
            gameDirector.GetType().GetProperty("GameData")
                .SetValue(gameDirector, gameData);

            MonoBehaviour sceneBoard = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.Board");
            sceneBoard.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(sceneBoard, null);

            MonoBehaviour sceneCardManager = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.CardManager");
            sceneCardManager.GetType().GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                .SetValue(null, sceneCardManager);
            sceneCardManager.GetType().GetMethod(
                    "InitializePrefabLookup",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(sceneCardManager, null);
            sceneCardManager.GetType().GetMethod(
                    "BuildDefinitionDatabase",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(sceneCardManager, null);

            MonoBehaviour controller = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.LocationSceneController");
            Object riverbendDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset");
            controller.GetType().GetField(
                    "activeDefinition",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, riverbendDefinition);
            System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            object playerData = System.Activator.CreateInstance(cardDataType);
            Object villagerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            cardDataType.GetField("Id").SetValue(
                playerData,
                new SerializedObject(villagerDefinition).FindProperty("id").stringValue);
            cardDataType.GetField("UsesLeft").SetValue(playerData, 3);
            cardDataType.GetField("CurrentHealth").SetValue(playerData, 7);
            cardDataType.GetField("CurrentNutrition").SetValue(playerData, 2);

            System.Array party = System.Array.CreateInstance(cardDataType, 1);
            party.SetValue(playerData, 0);
            MethodInfo replace = controller.GetType().GetMethod("ReplacePlayerParty");
            Assert.That(replace, Is.Not.Null);
            LogAssert.Expect(
                LogType.Error,
                "Instantiating material due to calling renderer.material during edit mode. This will leak materials into the scene. You most likely want to use renderer.sharedMaterial instead.");
            replace.Invoke(controller, new object[] { party });

            System.Type cardManagerType = FindType("CryingSnow.StackCraft.CardManager");
            MonoBehaviour cardManager = (MonoBehaviour)cardManagerType
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);
            Assert.That(cardManager, Is.Not.Null);
            var cards = ((System.Collections.IEnumerable)cardManager.GetType()
                    .GetProperty("AllCards").GetValue(cardManager))
                .Cast<Component>()
                .Where(card =>
                {
                    Object definition = (Object)card.GetType().GetProperty("Definition").GetValue(card);
                    var serialized = new SerializedObject(definition);
                    return serialized.FindProperty("category").enumValueIndex == 2 &&
                        serialized.FindProperty("faction").enumValueIndex == 1;
                })
                .ToList();

            Assert.That(cards, Has.Count.EqualTo(1));
            Component restored = cards[0];
            Assert.That(restored.GetType().GetProperty("CurrentHealth").GetValue(restored), Is.EqualTo(7));
            Assert.That(restored.GetType().GetProperty("CurrentNutrition").GetValue(restored), Is.EqualTo(2));
        }

        [Test]
        public void LocationEntrance_AcceptsOnlyPlayerCharacterStacks()
        {
            System.Type entranceType = FindType("CryingSnow.StackCraft.LocationEntrance");
            Assert.That(entranceType, Is.Not.Null);
            MethodInfo canAccept = entranceType.GetMethod(
                "CanAccept",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(canAccept, Is.Not.Null);

            Object playerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Object neutralDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Grocer.asset");
            Object structureDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Inn.asset");
            Component player = CreateUninitializedCard(playerDefinition, "Entrance Player");
            Component neutral = CreateUninitializedCard(neutralDefinition, "Entrance Neutral");
            Component structure = CreateUninitializedCard(structureDefinition, "Entrance Structure");
            try
            {
                object playerStack = player.GetType().GetProperty("Stack").GetValue(player);
                object neutralStack = neutral.GetType().GetProperty("Stack").GetValue(neutral);
                object structureStack = structure.GetType().GetProperty("Stack").GetValue(structure);

                Assert.That(canAccept.Invoke(null, new[] { playerStack }), Is.True);
                Assert.That(canAccept.Invoke(null, new[] { neutralStack }), Is.False);
                Assert.That(canAccept.Invoke(null, new[] { structureStack }), Is.False);
            }
            finally
            {
                DestroyTestCard(structure);
                DestroyTestCard(neutral);
                DestroyTestCard(player);
            }
        }

        [Test]
        public void LocationEntrance_DocksOnePlayerAndWaitsForExplicitEnterRequest()
        {
            System.Type entranceType = FindType("CryingSnow.StackCraft.LocationEntrance");
            Object playerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Object innDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Inn.asset");
            Component player = CreateUninitializedCard(playerDefinition, "Docked Entrance Player");
            Component inn = CreateUninitializedCard(innDefinition, "Dockable Inn Entrance");
            try
            {
                Component entrance = inn.gameObject.AddComponent(entranceType);
                entranceType.GetMethod("Configure").Invoke(entrance, new object[] { "riverbend-inn" });
                object playerStack = player.GetType().GetProperty("Stack").GetValue(player);

                Assert.That(entranceType.GetMethod("OnStack").Invoke(
                    entrance,
                    new[] { playerStack }), Is.True,
                    "放入人物槽只应完成收纳，不应依赖 GameDirector 立即切换场景");
                Assert.That(entranceType.GetProperty("Occupant").GetValue(entrance), Is.SameAs(player));
                Assert.That(entranceType.GetProperty("CanEnter").GetValue(entrance), Is.True);
                Assert.That(entranceType.GetMethod("TryEnter").Invoke(entrance, null), Is.False,
                    "没有 GameDirector 时显式进入请求应失败，证明堆叠本身没有触发切图");
            }
            finally
            {
                DestroyTestCard(inn);
                DestroyTestCard(player);
            }
        }

        [Test]
        public void LocationEntrance_SinglePersonSlotRejectsAnotherPlayerUntilDetached()
        {
            System.Type entranceType = FindType("CryingSnow.StackCraft.LocationEntrance");
            Object playerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Object innDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Inn.asset");
            Component firstPlayer = CreateUninitializedCard(playerDefinition, "First Slot Player");
            Component secondPlayer = CreateUninitializedCard(playerDefinition, "Second Slot Player");
            Component inn = CreateUninitializedCard(innDefinition, "Single Slot Inn");
            try
            {
                Component entrance = inn.gameObject.AddComponent(entranceType);
                entranceType.GetMethod("Configure").Invoke(entrance, new object[] { "riverbend-inn" });
                object firstStack = firstPlayer.GetType().GetProperty("Stack").GetValue(firstPlayer);
                object secondStack = secondPlayer.GetType().GetProperty("Stack").GetValue(secondPlayer);

                Assert.That(entranceType.GetMethod("OnStack").Invoke(entrance, new[] { firstStack }), Is.True);
                Assert.That(entranceType.GetMethod("OnStack").Invoke(entrance, new[] { secondStack }), Is.False);
                Assert.That(entranceType.GetProperty("Occupant").GetValue(entrance), Is.SameAs(firstPlayer));

                entranceType.GetMethod("Detach").Invoke(entrance, new object[] { firstPlayer });
                Assert.That(entranceType.GetMethod("OnStack").Invoke(entrance, new[] { secondStack }), Is.True);
                Assert.That(entranceType.GetProperty("Occupant").GetValue(entrance), Is.SameAs(secondPlayer));
            }
            finally
            {
                DestroyTestCard(inn);
                DestroyTestCard(secondPlayer);
                DestroyTestCard(firstPlayer);
            }
        }

        [Test]
        public void CardPhysicsSolver_TreatsBuildingSlotOccupantAsDockedStack()
        {
            System.Type entranceType = FindType("CryingSnow.StackCraft.LocationEntrance");
            System.Type solverType = FindType("CryingSnow.StackCraft.CardPhysicsSolver");
            Object playerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Object innDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Inn.asset");
            Component player = CreateUninitializedCard(playerDefinition, "Physics Docked Player");
            Component inn = CreateUninitializedCard(innDefinition, "Physics Docking Inn");
            try
            {
                Component entrance = inn.gameObject.AddComponent(entranceType);
                entranceType.GetMethod("Configure").Invoke(entrance, new object[] { "riverbend-inn" });
                object playerStack = player.GetType().GetProperty("Stack").GetValue(player);
                entranceType.GetMethod("OnStack").Invoke(entrance, new[] { playerStack });

                MethodInfo isDocked = solverType.GetMethod(
                    "IsDockedPartyStack",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(isDocked, Is.Not.Null);
                Assert.That(isDocked.Invoke(null, new[] { playerStack }), Is.True,
                    "建筑人物槽中的卡堆必须和世界地图停靠小队一样退出碰撞分离");
            }
            finally
            {
                DestroyTestCard(inn);
                DestroyTestCard(player);
            }
        }

        [Test]
        public void LocationEntrance_SelectionPersistsOnBlankAndClearsOnAnotherCard()
        {
            System.Type entranceType = FindType("CryingSnow.StackCraft.LocationEntrance");
            EventInfo selectionChanged = entranceType.GetEvent("SelectionChanged");
            Assert.That(selectionChanged, Is.Not.Null);
            Object innDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Inn.asset");
            Object playerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Component inn = CreateUninitializedCard(innDefinition, "Selectable Inn");
            Component otherCard = CreateUninitializedCard(playerDefinition, "Other Clicked Card");
            Component entrance = inn.gameObject.AddComponent(entranceType);
            object lastSelection = new object();
            System.Action<Component> handler = selected => lastSelection = selected;
            System.Delegate runtimeHandler = System.Delegate.CreateDelegate(
                selectionChanged.EventHandlerType,
                handler.Target,
                handler.Method);
            selectionChanged.AddEventHandler(null, runtimeHandler);
            try
            {
                entranceType.GetMethod("Configure").Invoke(entrance, new object[] { "riverbend-inn" });
                entranceType.GetMethod("SetSelected").Invoke(entrance, new object[] { true, true });
                Assert.That(lastSelection, Is.SameAs(entrance));

                entranceType.GetMethod("NotifyCardClicked", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { null });
                Assert.That(entranceType.GetProperty("IsSelected").GetValue(entrance), Is.True,
                    "点击空白区域不应取消建筑选择");

                entranceType.GetMethod("NotifyCardClicked", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { otherCard });
                Assert.That(entranceType.GetProperty("IsSelected").GetValue(entrance), Is.False);
                Assert.That(lastSelection, Is.Null);
            }
            finally
            {
                selectionChanged.RemoveEventHandler(null, runtimeHandler);
                DestroyTestCard(otherCard);
                DestroyTestCard(inn);
            }
        }

        [Test]
        public void OriginalUiRoot_SelectedBuildingShowsBuildingDetailsAndEnterButton()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            GameObject uiInstance = Object.Instantiate(prefab);
            Component player = null;
            Component inn = null;
            try
            {
                Component locationView = FindDescendant(uiInstance, "LocationView")
                    .GetComponents<MonoBehaviour>()
                    .First(component => component.GetType().FullName ==
                        "CryingSnow.StackCraft.WorldMapLocationView");
                locationView.GetType().GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(locationView, null);

                Object playerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
                Object innDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Inn.asset");
                player = CreateUninitializedCard(playerDefinition, "Sidebar Building Player");
                inn = CreateUninitializedCard(innDefinition, "Sidebar Inn Building");
                System.Type entranceType = FindType("CryingSnow.StackCraft.LocationEntrance");
                Component entrance = inn.gameObject.AddComponent(entranceType);
                entranceType.GetMethod("Configure").Invoke(entrance, new object[] { "riverbend-inn" });
                object playerStack = player.GetType().GetProperty("Stack").GetValue(player);
                entranceType.GetMethod("OnStack").Invoke(entrance, new[] { playerStack });
                entranceType.GetMethod("SetSelected").Invoke(entrance, new object[] { true, true });

                Assert.That(
                    FindDescendant(uiInstance, "LocationToggle").GetComponentInChildren<TMPro.TMP_Text>(true).text,
                    Is.EqualTo("建筑"));
                Assert.That(
                    FindDescendant(uiInstance, "LocationTitle").GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo("旅馆"));
                Assert.That(
                    FindDescendant(uiInstance, "LocationTypeAndDanger").GetComponent<TMPro.TMP_Text>().text,
                    Does.Contain("建筑"));
                Button enterButton = FindDescendant(uiInstance, "EnterLocationButton").GetComponent<Button>();
                Assert.That(enterButton.interactable, Is.True);
                Assert.That(enterButton.GetComponentInChildren<TMPro.TMP_Text>(true).text,
                    Is.EqualTo("进入旅馆"));
            }
            finally
            {
                Component locationView = FindDescendant(uiInstance, "LocationView")
                    ?.GetComponents<MonoBehaviour>()
                    .FirstOrDefault(component => component.GetType().FullName ==
                        "CryingSnow.StackCraft.WorldMapLocationView");
                locationView?.GetType().GetMethod(
                    "OnDestroy",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(locationView, null);
                DestroyTestCard(inn);
                DestroyTestCard(player);
                Object.DestroyImmediate(uiInstance);
            }
        }

        [Test]
        public void RiverbendAndInn_NpcCardsAreNotPlayerDraggable()
        {
            string[] locationPaths =
            {
                "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset",
                "Assets/StackCraft/Resources/Locations/Location_RiverbendInn.asset"
            };

            foreach (string locationPath in locationPaths)
            {
                Object location = AssetDatabase.LoadAssetAtPath<Object>(locationPath);
                SerializedProperty spawns = new SerializedObject(location)
                    .FindProperty("initialCardSpawns");
                for (int index = 0; index < spawns.arraySize; index++)
                {
                    Object definition = spawns.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("definition").objectReferenceValue;
                    var serializedCard = new SerializedObject(definition);
                    bool isNeutralCharacter =
                        serializedCard.FindProperty("category").enumValueIndex == 2 &&
                        serializedCard.FindProperty("faction").enumValueIndex == 0;
                    if (!isNeutralCharacter)
                        continue;

                    Assert.That(serializedCard.FindProperty("playerDraggable").boolValue, Is.False,
                        $"{serializedCard.FindProperty("displayName").stringValue} 是 NPC，不能被玩家拖动");
                }
            }
        }

        [Test]
        public void LocationEntrance_FindsConfiguredEntranceNearDroppedPlayer()
        {
            System.Type entranceType = FindType("CryingSnow.StackCraft.LocationEntrance");
            MethodInfo findNearby = entranceType.GetMethod(
                "FindNearby",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(findNearby, Is.Not.Null);

            Object playerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Object innDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Inn.asset");
            Component player = CreateUninitializedCard(playerDefinition, "Nearby Entrance Player");
            Component inn = CreateUninitializedCard(innDefinition, "Nearby Inn Entrance");
            try
            {
                player.gameObject.AddComponent<BoxCollider>();
                inn.gameObject.AddComponent<BoxCollider>();
                player.transform.position = Vector3.zero;
                inn.transform.position = new Vector3(0.5f, 0f, 0f);
                Component entrance = inn.gameObject.AddComponent(entranceType);
                entranceType.GetMethod("Configure").Invoke(entrance, new object[] { "riverbend-inn" });
                Physics.SyncTransforms();

                Assert.That(
                    findNearby.Invoke(null, new object[] { player, 1.25f }),
                    Is.SameAs(entrance));

                inn.transform.position = new Vector3(5f, 0f, 0f);
                Physics.SyncTransforms();
                Assert.That(findNearby.Invoke(null, new object[] { player, 1.25f }), Is.Null);
            }
            finally
            {
                DestroyTestCard(inn);
                DestroyTestCard(player);
            }
        }

        [Test]
        public void CardController_DocksAtBuildingOnlyWithinNormalCardAttachRadius()
        {
            System.Type controllerType = FindType("CryingSnow.StackCraft.CardController");
            System.Type entranceType = FindType("CryingSnow.StackCraft.LocationEntrance");
            Object playerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Object innDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Inn.asset");
            Component player = CreateUninitializedCard(playerDefinition, "Attach Radius Player");
            Component inn = CreateUninitializedCard(innDefinition, "Attach Radius Inn");
            try
            {
                Component controller = player.gameObject.AddComponent(controllerType);
                controllerType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                Component entrance = inn.gameObject.AddComponent(entranceType);
                entranceType.GetMethod("Configure").Invoke(
                    entrance,
                    new object[] { "riverbend-inn" });
                MethodInfo tryDock = controllerType.GetMethod(
                    "TryDockAtNearbyBuilding",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(tryDock, Is.Not.Null);

                player.transform.position = Vector3.zero;
                inn.transform.position = new Vector3(0.9f, 0f, 0f);
                Physics.SyncTransforms();

                Assert.That(tryDock.Invoke(controller, null), Is.False,
                    "建筑人物槽不能在普通卡牌堆叠范围之外吸入人物卡");
                Assert.That(entranceType.GetProperty("Occupant").GetValue(entrance), Is.Null);

                inn.transform.position = new Vector3(0.6f, 0f, 0f);
                Physics.SyncTransforms();
                Assert.That(tryDock.Invoke(controller, null), Is.True,
                    "人物卡进入普通卡牌堆叠范围后应能放入建筑人物槽");
                Assert.That(entranceType.GetProperty("Occupant").GetValue(entrance), Is.SameAs(player));
            }
            finally
            {
                DestroyTestCard(inn);
                DestroyTestCard(player);
            }
        }

        [Test]
        public void LocationSceneController_ConfiguresInnBuildingAsRiverbendEntrance()
        {
            Object riverbend = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset");
            Object innDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Inn.asset");
            Assert.That(riverbend, Is.Not.Null);
            Assert.That(innDefinition, Is.Not.Null);

            Component innCard = CreateUninitializedCard(innDefinition, "Configured Inn Entrance");
            try
            {
                System.Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
                System.Type controllerType = FindType("CryingSnow.StackCraft.LocationSceneController");
                System.Type locationDefinitionType = FindType("CryingSnow.StackCraft.LocationDefinition");
                System.Array cards = System.Array.CreateInstance(cardType, 1);
                cards.SetValue(innCard, 0);
                MethodInfo configure = controllerType.GetMethod(
                    "ConfigureLocationCardBehaviours",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[]
                    {
                        typeof(IEnumerable<>).MakeGenericType(cardType),
                        locationDefinitionType
                    },
                    null);
                Assert.That(configure, Is.Not.Null);
                configure.Invoke(null, new object[] { cards, riverbend });

                Component entrance = innCard.GetComponent("LocationEntrance");
                Assert.That(entrance, Is.Not.Null);
                Assert.That(
                    entrance.GetType().GetProperty("DestinationLocationId").GetValue(entrance),
                    Is.EqualTo("riverbend-inn"));
            }
            finally
            {
                DestroyTestCard(innCard);
            }
        }

        [Test]
        public void LocationScene_RegistersRiverbendAndInnDefinitions()
        {
            EditorSceneManager.OpenScene(
                "Assets/StackCraft/Scenes/Location.unity",
                OpenSceneMode.Single);
            MonoBehaviour controller = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.LocationSceneController");
            Assert.That(controller, Is.Not.Null);

            SerializedProperty definitions = new SerializedObject(controller)
                .FindProperty("locationDefinitions");
            var ids = new List<string>();
            for (int index = 0; index < definitions.arraySize; index++)
            {
                Object definition = definitions.GetArrayElementAtIndex(index).objectReferenceValue;
                if (definition != null)
                    ids.Add(new SerializedObject(definition).FindProperty("id").stringValue);
            }

            Assert.That(ids, Does.Contain("riverbend"));
            Assert.That(ids, Does.Contain("riverbend-inn"));
        }

        [Test]
        public void LocationSceneController_MergesConfiguredAndDiscoveredDefinitionsById()
        {
            System.Type definitionType = FindType("CryingSnow.StackCraft.LocationDefinition");
            System.Type controllerType = FindType("CryingSnow.StackCraft.LocationSceneController");
            ScriptableObject configuredRiverbend =
                ScriptableObject.CreateInstance(definitionType);
            ScriptableObject discoveredRiverbend =
                ScriptableObject.CreateInstance(definitionType);
            ScriptableObject discoveredForest =
                ScriptableObject.CreateInstance(definitionType);
            try
            {
                SetSerializedString(configuredRiverbend, "id", "riverbend");
                SetSerializedString(discoveredRiverbend, "id", "riverbend");
                SetSerializedString(discoveredForest, "id", "whispering-forest");

                System.Array configured = System.Array.CreateInstance(definitionType, 2);
                configured.SetValue(configuredRiverbend, 0);
                configured.SetValue(null, 1);
                System.Array discovered = System.Array.CreateInstance(definitionType, 2);
                discovered.SetValue(discoveredRiverbend, 0);
                discovered.SetValue(discoveredForest, 1);

                MethodInfo merge = controllerType.GetMethod(
                    "MergeDefinitions",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.That(merge, Is.Not.Null,
                    "通用地点场景需要自动合并场景配置和 Resources/Locations 中的新地点");

                var merged = ((IEnumerable)merge.Invoke(
                        null,
                        new object[] { configured, discovered }))
                    .Cast<Object>()
                    .ToList();
                Assert.That(merged.Count, Is.EqualTo(2));
                Assert.That(merged[0], Is.SameAs(configuredRiverbend),
                    "同一地点 ID 应保留显式场景配置，避免旧地图引用被替换");
                Assert.That(merged, Does.Contain(discoveredForest));
            }
            finally
            {
                Object.DestroyImmediate(discoveredForest);
                Object.DestroyImmediate(discoveredRiverbend);
                Object.DestroyImmediate(configuredRiverbend);
            }
        }

        [Test]
        public void LocationTemplateBuilder_CreatesReusableLocationDefinitionAsset()
        {
            const string assetPath =
                "Assets/CardColony/Tests/UnityEditMode/Temp_LocationTemplate.asset";
            AssetDatabase.DeleteAsset(assetPath);

            System.Type builderType = FindType(
                "CryingSnow.StackCraft.EditorTools.LocationTemplateBuilder");
            System.Type templateType = FindType(
                "CryingSnow.StackCraft.EditorTools.LocationTemplate");
            Assert.That(builderType, Is.Not.Null);
            Assert.That(templateType, Is.Not.Null);

            object template = System.Activator.CreateInstance(templateType);
            templateType.GetField("Id").SetValue(template, "template-test");
            templateType.GetField("DisplayName").SetValue(template, "模板测试地点");
            templateType.GetField("MapSize").SetValue(template, new Vector2(22f, 14f));
            templateType.GetField("CameraMinDistance").SetValue(template, 4f);
            templateType.GetField("CameraMaxDistance").SetValue(template, 26f);
            templateType.GetField("CameraInitialDistance").SetValue(template, 9f);
            templateType.GetField("CameraZoomSpeed").SetValue(template, 3.5f);
            templateType.GetField("PartySpawnPosition").SetValue(
                template,
                new Vector3(-1f, 0f, -3f));
            templateType.GetField("PartyMemberSpacing").SetValue(template, 1.1f);

            MethodInfo createOrUpdate = builderType.GetMethod(
                "CreateOrUpdate",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(createOrUpdate, Is.Not.Null);

            try
            {
                Object definition = (Object)createOrUpdate.Invoke(
                    null,
                    new[] { assetPath, template });
                Assert.That(definition, Is.Not.Null);
                Assert.That(AssetDatabase.LoadAssetAtPath<Object>(assetPath), Is.SameAs(definition));

                var serialized = new SerializedObject(definition);
                Assert.That(serialized.FindProperty("id").stringValue, Is.EqualTo("template-test"));
                Assert.That(serialized.FindProperty("displayName").stringValue,
                    Is.EqualTo("模板测试地点"));
                Assert.That(serialized.FindProperty("mapSize").vector2Value,
                    Is.EqualTo(new Vector2(22f, 14f)));
                Assert.That(serialized.FindProperty("cameraZoomSpeed").floatValue,
                    Is.EqualTo(3.5f));
                Assert.That(serialized.FindProperty("partySpawnPosition").vector3Value,
                    Is.EqualTo(new Vector3(-1f, 0f, -3f)));
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void LocationTemplateBuilder_UpsertsEntrancesWithoutDeletingExistingEntries()
        {
            const string assetPath =
                "Assets/CardColony/Tests/UnityEditMode/Temp_LocationEntrances.asset";
            AssetDatabase.DeleteAsset(assetPath);

            System.Type builderType = FindType(
                "CryingSnow.StackCraft.EditorTools.LocationTemplateBuilder");
            System.Type templateType = FindType(
                "CryingSnow.StackCraft.EditorTools.LocationTemplate");
            System.Type entranceType = FindType(
                "CryingSnow.StackCraft.EditorTools.LocationTemplateEntrance");
            MethodInfo createOrUpdate = builderType.GetMethod(
                "CreateOrUpdate",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo upsertEntrance = builderType.GetMethod(
                "UpsertEntrance",
                BindingFlags.Public | BindingFlags.Static);
            Object market = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Market.asset");
            Object inn = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Inn.asset");

            object template = System.Activator.CreateInstance(templateType);
            templateType.GetField("Id").SetValue(template, "entrance-template-test");
            templateType.GetField("DisplayName").SetValue(template, "入口模板测试");

            try
            {
                Object definition = (Object)createOrUpdate.Invoke(
                    null,
                    new object[] { assetPath, template });
                upsertEntrance.Invoke(
                    null,
                    new object[] { definition, market, "future-market" });

                object newEntrance = System.Activator.CreateInstance(
                    entranceType,
                    new object[] { inn, "riverbend-inn" });
                System.Array entrances = System.Array.CreateInstance(entranceType, 1);
                entrances.SetValue(newEntrance, 0);
                templateType.GetField("Entrances").SetValue(template, entrances);

                createOrUpdate.Invoke(null, new object[] { assetPath, template });

                SerializedProperty savedEntrances = new SerializedObject(definition)
                    .FindProperty("entrances");
                var destinations = new List<string>();
                for (int index = 0; index < savedEntrances.arraySize; index++)
                {
                    destinations.Add(savedEntrances.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("destinationLocationId").stringValue);
                }

                Assert.That(destinations, Is.EquivalentTo(new[]
                {
                    "future-market",
                    "riverbend-inn"
                }), "模板更新应追加或更新声明的入口，不能删除其他系统后来添加的入口");
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void LocationTemplateBuilder_PreservesExplicitSceneDefinitionWithSameId()
        {
            System.Type definitionType = FindType("CryingSnow.StackCraft.LocationDefinition");
            System.Type controllerType = FindType("CryingSnow.StackCraft.LocationSceneController");
            System.Type builderType = FindType(
                "CryingSnow.StackCraft.EditorTools.LocationTemplateBuilder");
            ScriptableObject explicitDefinition =
                ScriptableObject.CreateInstance(definitionType);
            ScriptableObject generatedDefinition =
                ScriptableObject.CreateInstance(definitionType);
            var controllerObject = new GameObject("Location Template Controller");
            Component controller = controllerObject.AddComponent(controllerType);
            try
            {
                SetSerializedString(explicitDefinition, "id", "same-location");
                SetSerializedString(generatedDefinition, "id", "same-location");
                var serializedController = new SerializedObject(controller);
                SerializedProperty definitions =
                    serializedController.FindProperty("locationDefinitions");
                definitions.InsertArrayElementAtIndex(0);
                definitions.GetArrayElementAtIndex(0).objectReferenceValue =
                    explicitDefinition;
                serializedController.ApplyModifiedPropertiesWithoutUndo();

                builderType.GetMethod(
                        "UpsertDefinition",
                        BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { controller, generatedDefinition });

                serializedController.Update();
                Assert.That(definitions.arraySize, Is.EqualTo(1));
                Assert.That(
                    definitions.GetArrayElementAtIndex(0).objectReferenceValue,
                    Is.SameAs(explicitDefinition),
                    "安装器不能替换场景中同 ID 的显式覆盖定义");
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(generatedDefinition);
                Object.DestroyImmediate(explicitDefinition);
            }
        }

        [Test]
        public void LocationTemplateBuilder_GeneratesDifferentDefaultIdsForUniqueAssetPaths()
        {
            System.Type builderType = FindType(
                "CryingSnow.StackCraft.EditorTools.LocationTemplateBuilder");
            MethodInfo createId = builderType.GetMethod(
                "CreateDefaultLocationId",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(createId, Is.Not.Null);

            string first = (string)createId.Invoke(
                null,
                new object[] { "Assets/StackCraft/Resources/Locations/Location_New.asset" });
            string second = (string)createId.Invoke(
                null,
                new object[] { "Assets/StackCraft/Resources/Locations/Location_New 1.asset" });

            Assert.That(first, Is.Not.Empty);
            Assert.That(second, Is.Not.Empty);
            Assert.That(second, Is.Not.EqualTo(first),
                "连续创建地点时，唯一资产路径也必须产生唯一的默认地点 ID");
        }

        [Test]
        public void LocationSceneController_AutoDiscoversDefinitionFromResourcesFolder()
        {
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/StackCraft/Resources/Locations/Location_AutoDiscoveryTest.asset");
            System.Type builderType = FindType(
                "CryingSnow.StackCraft.EditorTools.LocationTemplateBuilder");
            System.Type templateType = FindType(
                "CryingSnow.StackCraft.EditorTools.LocationTemplate");
            object template = System.Activator.CreateInstance(templateType);
            templateType.GetField("Id").SetValue(template, "auto-discovery-test");
            templateType.GetField("DisplayName").SetValue(template, "自动发现测试");
            var controllerObject = new GameObject("Auto Discovery Controller");
            Component controller = controllerObject.AddComponent(
                FindType("CryingSnow.StackCraft.LocationSceneController"));
            try
            {
                builderType.GetMethod(
                        "CreateOrUpdate",
                        BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { assetPath, template });
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                MethodInfo getAvailable = controller.GetType().GetMethod(
                    "GetAvailableDefinitions",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var ids = ((IEnumerable)getAvailable.Invoke(controller, null))
                    .Cast<Object>()
                    .Select(definition =>
                        new SerializedObject(definition).FindProperty("id").stringValue)
                    .ToList();
                Assert.That(ids, Does.Contain("auto-discovery-test"));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        [Test]
        public void LocationSceneController_ReturnLabelNamesParentLocation()
        {
            System.Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            System.Type controllerType = FindType("CryingSnow.StackCraft.LocationSceneController");
            System.Type definitionType = FindType("CryingSnow.StackCraft.LocationDefinition");
            object gameData = System.Activator.CreateInstance(gameDataType);
            gameDataType.GetMethod("PushLocation").Invoke(gameData, new object[] { "riverbend" });

            Object riverbend = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset");
            Object inn = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_RiverbendInn.asset");
            System.Array definitions = System.Array.CreateInstance(definitionType, 2);
            definitions.SetValue(riverbend, 0);
            definitions.SetValue(inn, 1);

            MethodInfo getLabel = controllerType.GetMethod(
                "GetReturnButtonLabel",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(getLabel, Is.Not.Null);
            Assert.That(
                getLabel.Invoke(null, new object[] { gameData, definitions }),
                Is.EqualTo("返回河湾村"));

            object[] popArguments = { null };
            gameDataType.GetMethod("TryPopLocation").Invoke(gameData, popArguments);
            Assert.That(
                getLabel.Invoke(null, new object[] { gameData, definitions }),
                Is.EqualTo("返回世界地图"));
        }

        [Test]
        public void RiverbendInnInstaller_UpsertsWithoutDeletingFutureLocationContent()
        {
            System.Type installerType = FindType(
                "CryingSnow.StackCraft.EditorTools.RiverbendInnInstaller");
            MethodInfo upsertEntrance = installerType?.GetMethod(
                "UpsertEntrance",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo upsertDefinition = installerType?.GetMethod(
                "UpsertDefinition",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(upsertEntrance, Is.Not.Null);
            Assert.That(upsertDefinition, Is.Not.Null);

            System.Type definitionType = FindType("CryingSnow.StackCraft.LocationDefinition");
            ScriptableObject temporaryLocation = ScriptableObject.CreateInstance(definitionType);
            GameObject controllerObject = new GameObject("Temporary Location Controller");
            Component controller = controllerObject.AddComponent(
                FindType("CryingSnow.StackCraft.LocationSceneController"));
            try
            {
                Object market = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Market.asset");
                Object innBuilding = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Inn.asset");
                Object riverbend = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset");
                Object inn = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/StackCraft/Resources/Locations/Location_RiverbendInn.asset");

                var serializedLocation = new SerializedObject(temporaryLocation);
                SerializedProperty entrances = serializedLocation.FindProperty("entrances");
                entrances.InsertArrayElementAtIndex(0);
                entrances.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("sourceCardDefinition").objectReferenceValue = market;
                entrances.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("destinationLocationId").stringValue = "future-market";
                serializedLocation.ApplyModifiedPropertiesWithoutUndo();

                upsertEntrance.Invoke(
                    null,
                    new object[] { temporaryLocation, innBuilding, "riverbend-inn" });
                serializedLocation.Update();
                Assert.That(entrances.arraySize, Is.EqualTo(2));
                Assert.That(entrances.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("destinationLocationId").stringValue, Is.EqualTo("future-market"));

                var serializedController = new SerializedObject(controller);
                SerializedProperty definitions = serializedController.FindProperty("locationDefinitions");
                definitions.InsertArrayElementAtIndex(0);
                definitions.GetArrayElementAtIndex(0).objectReferenceValue = riverbend;
                serializedController.ApplyModifiedPropertiesWithoutUndo();

                upsertDefinition.Invoke(null, new object[] { controller, inn });
                upsertDefinition.Invoke(null, new object[] { controller, inn });
                serializedController.Update();
                Assert.That(definitions.arraySize, Is.EqualTo(2));
                Assert.That(definitions.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(riverbend));
                Assert.That(definitions.GetArrayElementAtIndex(1).objectReferenceValue, Is.SameAs(inn));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(temporaryLocation);
            }
        }

        [Test]
        public void WhisperingForest_IsConfiguredAsEnterableRandomizedLocation()
        {
            Object forest = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Locations/Location_WhisperingForest.asset");
            Assert.That(forest, Is.Not.Null, "低语森林需要使用通用 LocationDefinition 资产");

            var serialized = new SerializedObject(forest);
            Assert.That(serialized.FindProperty("id").stringValue,
                Is.EqualTo("whispering-forest"));
            Assert.That(
                AssetDatabase.GetAssetPath(
                    serialized.FindProperty("backgroundTexture").objectReferenceValue),
                Is.EqualTo(
                    "Assets/CardColony/Art/Backgrounds/WhisperingForestBackground_v2.png"));
            Assert.That(serialized.FindProperty("randomizeCardsOnEntry").boolValue,
                Is.True, "野外地点需要在每次从世界地图进入时重新生成内容");
            Vector2 mapSize = serialized.FindProperty("mapSize").vector2Value;
            Vector2 randomCenter =
                serialized.FindProperty("randomSpawnAreaCenter").vector2Value;
            Vector2 randomSize =
                serialized.FindProperty("randomSpawnAreaSize").vector2Value;
            const float boardTopMargin = 1.5f;
            const float standardCardHalfDepth = 0.6f;
            Assert.That(
                randomCenter.y + randomSize.y * 0.5f,
                Is.LessThanOrEqualTo(
                    mapSize.y * 0.5f - boardTopMargin - standardCardHalfDepth),
                "随机区域需要避开顶部 UI 禁放区，并为完整卡牌尺寸留出空间");

            SerializedProperty rules = serialized.FindProperty("randomCardSpawns");
            Assert.That(rules, Is.Not.Null);
            var cardAssetNames = new HashSet<string>();
            for (int index = 0; index < rules.arraySize; index++)
            {
                Object definition = rules.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("definition").objectReferenceValue;
                Assert.That(definition, Is.Not.Null);
                cardAssetNames.Add(definition.name);
            }

            Assert.That(cardAssetNames, Is.SupersetOf(new[]
            {
                "Card_Rock",
                "Card_BerryBush",
                "Card_Tree",
                "Card_Slime",
                "Card_Goblin"
            }), "低语森林应直接复用原项目的资源与敌人卡牌");

            EditorSceneManager.OpenScene(
                "Assets/StackCraft/Scenes/Main.unity",
                OpenSceneMode.Single);
            MonoBehaviour bootstrap = Object.FindObjectsOfType<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.WorldMapBootstrap");
            Assert.That(bootstrap, Is.Not.Null);

            SerializedProperty locations = new SerializedObject(bootstrap)
                .FindProperty("locationDetails");
            bool isEnterable = false;
            for (int index = 0; index < locations.arraySize; index++)
            {
                SerializedProperty location = locations.GetArrayElementAtIndex(index);
                if (location.FindPropertyRelative("locationId").stringValue !=
                    "whispering-forest")
                {
                    continue;
                }

                isEnterable = location.FindPropertyRelative("localMapImplemented").boolValue;
                break;
            }

            Assert.That(isEnterable, Is.True,
                "世界地图中的低语森林卡牌需要开放“进入地点”按钮");
        }

        [Test]
        public void LocationSceneController_RandomSpawnPlanIsSeededAndRespectsRuleCounts()
        {
            System.Type definitionType = FindType("CryingSnow.StackCraft.LocationDefinition");
            System.Type controllerType = FindType(
                "CryingSnow.StackCraft.LocationSceneController");
            ScriptableObject definition =
                ScriptableObject.CreateInstance(definitionType);
            try
            {
                Object rock = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/StackCraft/Resources/Cards/Resources/Card_Rock.asset");
                Object goblin = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/StackCraft/Resources/Cards/Mobs/Card_Goblin.asset");
                var serialized = new SerializedObject(definition);
                SerializedProperty rules = serialized.FindProperty("randomCardSpawns");
                Assert.That(rules, Is.Not.Null,
                    "通用地点定义需要声明可复用的随机卡牌规则");
                rules.arraySize = 2;
                SetRandomSpawnRule(rules.GetArrayElementAtIndex(0), rock, 2, 4);
                SetRandomSpawnRule(rules.GetArrayElementAtIndex(1), goblin, 0, 2);
                serialized.FindProperty("randomSpawnAreaCenter").vector2Value =
                    new Vector2(1f, -0.5f);
                serialized.FindProperty("randomSpawnAreaSize").vector2Value =
                    new Vector2(12f, 6f);
                serialized.FindProperty("randomSpawnMinSpacing").floatValue = 0.8f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                MethodInfo createPlan = controllerType.GetMethod(
                    "CreateRandomSpawnPlan",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { definitionType, typeof(int) },
                    null);
                Assert.That(createPlan, Is.Not.Null,
                    "随机布局应由可测试的种子计划生成器创建");

                List<object> first = ((IEnumerable)createPlan.Invoke(
                        null,
                        new object[] { definition, 12345 }))
                    .Cast<object>()
                    .ToList();
                List<object> repeated = ((IEnumerable)createPlan.Invoke(
                        null,
                        new object[] { definition, 12345 }))
                    .Cast<object>()
                    .ToList();
                List<object> different = ((IEnumerable)createPlan.Invoke(
                        null,
                        new object[] { definition, 54321 }))
                    .Cast<object>()
                    .ToList();

                Assert.That(CaptureRandomSpawnPlan(first),
                    Is.EqualTo(CaptureRandomSpawnPlan(repeated)),
                    "同一种子必须生成可复现的布局，方便测试与排查存档");
                Assert.That(CaptureRandomSpawnPlan(first),
                    Is.Not.EqualTo(CaptureRandomSpawnPlan(different)),
                    "不同进入种子应能改变卡牌数量或位置");

                int rockCount = CountRandomSpawnItems(first, "Card_Rock");
                int goblinCount = CountRandomSpawnItems(first, "Card_Goblin");
                Assert.That(rockCount, Is.InRange(2, 4));
                Assert.That(goblinCount, Is.InRange(0, 2));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void LocationSceneController_RandomSpawnPlanAvoidsRuntimeOccupancyAndSkipsInvalidFallback()
        {
            System.Type definitionType = FindType("CryingSnow.StackCraft.LocationDefinition");
            System.Type controllerType = FindType(
                "CryingSnow.StackCraft.LocationSceneController");
            ScriptableObject definition =
                ScriptableObject.CreateInstance(definitionType);
            try
            {
                Object rock = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/StackCraft/Resources/Cards/Resources/Card_Rock.asset");
                var serialized = new SerializedObject(definition);
                SerializedProperty rules = serialized.FindProperty("randomCardSpawns");
                rules.arraySize = 1;
                SetRandomSpawnRule(rules.GetArrayElementAtIndex(0), rock, 1, 1);
                serialized.FindProperty("randomSpawnAreaCenter").vector2Value =
                    Vector2.zero;
                serialized.FindProperty("randomSpawnAreaSize").vector2Value =
                    Vector2.zero;
                serialized.FindProperty("randomSpawnMinSpacing").floatValue = 1f;
                serialized.FindProperty("randomSpawnPartyClearance").floatValue = 2f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                MethodInfo createPlan = controllerType.GetMethod(
                    "CreateRandomSpawnPlan",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[]
                    {
                        definitionType,
                        typeof(int),
                        typeof(IEnumerable<Vector3>),
                        typeof(IEnumerable<Vector3>)
                    },
                    null);
                Assert.That(createPlan, Is.Not.Null,
                    "重进地点的布局计划必须接收保留卡牌和所有队员的运行时位置");

                var occupied = new[] { Vector3.zero };
                var party = new[] { new Vector3(4f, 0f, 0f) };
                List<object> blocked = ((IEnumerable)createPlan.Invoke(
                        null,
                        new object[] { definition, 7, occupied, party }))
                    .Cast<object>()
                    .ToList();
                Assert.That(blocked, Is.Empty,
                    "找不到合法位置时应跳过卡牌，不能返回已知碰撞的最后候选点");

                serialized.FindProperty("randomSpawnAreaSize").vector2Value =
                    new Vector2(10f, 6f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                List<object> valid = ((IEnumerable)createPlan.Invoke(
                        null,
                        new object[] { definition, 7, occupied, party }))
                    .Cast<object>()
                    .ToList();
                Assert.That(valid, Has.Count.EqualTo(1));
                Vector3 position = (Vector3)valid[0].GetType()
                    .GetProperty("Position").GetValue(valid[0]);
                Assert.That(Vector3.Distance(position, occupied[0]),
                    Is.GreaterThanOrEqualTo(1f));
                Assert.That(Vector3.Distance(position, party[0]),
                    Is.GreaterThanOrEqualTo(2f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void CardData_PreservesRandomLocationSpawnIdentity()
        {
            Object rock = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Resources/Card_Rock.asset");
            Component card = CreateUninitializedCard(rock, "Random Forest Rock");
            try
            {
                System.Type markerType = FindType(
                    "CryingSnow.StackCraft.LocationRandomSpawnMarker");
                Assert.That(markerType, Is.Not.Null,
                    "随机地点卡牌需要持久化标记，重进地点时才能只替换随机内容");
                card.gameObject.AddComponent(markerType);

                System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
                object cardData = System.Activator.CreateInstance(
                    cardDataType,
                    new object[] { card });
                FieldInfo markerField = cardDataType.GetField("IsLocationRandomSpawn");
                Assert.That(markerField, Is.Not.Null);
                Assert.That(markerField.GetValue(cardData), Is.True);
            }
            finally
            {
                DestroyTestCard(card);
            }
        }

        [Test]
        public void CardManager_RestoresRandomLocationSpawnMarker()
        {
            EditorSceneManager.OpenScene(
                "Assets/StackCraft/Scenes/Location.unity",
                OpenSceneMode.Single);
            System.Type gameDirectorType = FindType("CryingSnow.StackCraft.GameDirector");
            var gameDirectorObject = new GameObject("Random Spawn Game Director");
            Component gameDirector = gameDirectorObject.AddComponent(gameDirectorType);
            gameDirectorType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                .SetValue(null, gameDirector);
            System.Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            object gameData = System.Activator.CreateInstance(gameDataType);
            gameDataType.GetField("GameplayPrefs").SetValue(
                gameData,
                System.Activator.CreateInstance(
                    FindType("CryingSnow.StackCraft.GameplayPrefs")));
            gameDirectorType.GetProperty("GameData").SetValue(gameDirector, gameData);

            MonoBehaviour board = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.Board");
            board.GetType().GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(board, null);
            MonoBehaviour cardManager = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.CardManager");
            cardManager.GetType().GetProperty(
                    "Instance",
                    BindingFlags.Public | BindingFlags.Static)
                .SetValue(null, cardManager);
            Component restored = null;
            try
            {
                cardManager.GetType().GetMethod(
                        "InitializePrefabLookup",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(cardManager, null);
                cardManager.GetType().GetMethod(
                        "BuildDefinitionDatabase",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(cardManager, null);

                Object rock = AssetDatabase.LoadAssetAtPath<Object>(
                    "Assets/StackCraft/Resources/Cards/Resources/Card_Rock.asset");
                System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
                object data = System.Activator.CreateInstance(cardDataType);
                cardDataType.GetField("Id").SetValue(
                    data,
                    rock.GetType().GetProperty("Id").GetValue(rock));
                cardDataType.GetField("UsesLeft").SetValue(data, 3);
                cardDataType.GetField("CurrentHealth").SetValue(data, 15);
                cardDataType.GetField("IsLocationRandomSpawn").SetValue(data, true);

                LogAssert.Expect(
                    LogType.Error,
                    "Instantiating material due to calling renderer.material during edit mode. This will leak materials into the scene. You most likely want to use renderer.sharedMaterial instead.");
                restored = (Component)cardManager.GetType()
                    .GetMethod("RestoreCardFromData")
                    .Invoke(cardManager, new object[] { data, Vector3.zero, false });
                Assert.That(restored, Is.Not.Null);
                Assert.That(
                    restored.GetComponent("LocationRandomSpawnMarker"),
                    Is.Not.Null,
                    "从存档恢复地点时，随机卡牌必须保留可重投标记");
            }
            finally
            {
                if (restored != null)
                    Object.DestroyImmediate(restored.gameObject);
                Object.DestroyImmediate(gameDirectorObject);
            }
        }

        [Test]
        public void LocationSceneController_RandomizesOnlyForWorldMapEntryOrFreshLocation()
        {
            System.Type controllerType = FindType(
                "CryingSnow.StackCraft.LocationSceneController");
            System.Type reasonType = FindType(
                "CryingSnow.StackCraft.LocationTransitionReason");
            MethodInfo shouldRandomize = controllerType.GetMethod(
                "ShouldRandomizeLocationCards",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(shouldRandomize, Is.Not.Null);
            Assert.That(reasonType, Is.Not.Null);

            object none = System.Enum.Parse(reasonType, "None");
            object worldMapEntry = System.Enum.Parse(reasonType, "WorldMapEntry");
            object childEntry = System.Enum.Parse(reasonType, "ChildLocationEntry");
            object returnToParent = System.Enum.Parse(reasonType, "ReturnToParent");

            Assert.That(
                shouldRandomize.Invoke(null, new[] { (object)false, false, none }),
                Is.False,
                "未开启随机地点内容时不能刷新卡牌");
            Assert.That(
                shouldRandomize.Invoke(
                    null,
                    new[] { (object)true, true, worldMapEntry }),
                Is.True,
                "从世界地图重新进入已保存地点时需要刷新随机内容");
            Assert.That(
                shouldRandomize.Invoke(
                    null,
                    new[] { (object)true, true, childEntry }),
                Is.False,
                "进入子地点不能刷新当前地点");
            Assert.That(
                shouldRandomize.Invoke(
                    null,
                    new[] { (object)true, true, returnToParent }),
                Is.False,
                "从建筑或下一层返回父地点不能刷新父地点");
            Assert.That(
                shouldRandomize.Invoke(null, new[] { (object)true, true, none }),
                Is.False,
                "在地点中直接读取存档不能刷新敌人与资源");
            Assert.That(
                shouldRandomize.Invoke(null, new[] { (object)true, false, none }),
                Is.True,
                "首次进入未保存地点时需要生成随机内容");
        }

        [Test]
        public void CombatManager_EndAllCombatsRestoresCombatantsToWorldStacks()
        {
            EditorSceneManager.OpenScene(
                "Assets/StackCraft/Scenes/Location.unity",
                OpenSceneMode.Single);
            foreach (string typeName in new[]
                     {
                         "CryingSnow.StackCraft.Board",
                         "CryingSnow.StackCraft.CardManager",
                         "CryingSnow.StackCraft.CombatManager"
                     })
            {
                MonoBehaviour singleton = Object.FindObjectsOfType<MonoBehaviour>(true)
                    .First(component => component.GetType().FullName == typeName);
                singleton.GetType().GetMethod(
                        "Awake",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(singleton, null);
            }

            MonoBehaviour combatManager = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.CombatManager");
            MethodInfo endAll = combatManager.GetType().GetMethod(
                "EndAllCombats",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(endAll, Is.Not.Null,
                "地点重投前需要通过战斗系统正式结束所有活动战斗");

            Object playerDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset");
            Object goblinDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Mobs/Card_Goblin.asset");
            Component player = CreateUninitializedCard(playerDefinition, "Combat Player");
            Component goblin = CreateUninitializedCard(goblinDefinition, "Combat Goblin");
            try
            {
                InitializeTestCombatant(player);
                InitializeTestCombatant(goblin);
                System.Type cardType = player.GetType();
                System.Type listType = typeof(List<>).MakeGenericType(cardType);
                var attackers = (IList)System.Activator.CreateInstance(listType);
                var defenders = (IList)System.Activator.CreateInstance(listType);
                attackers.Add(player);
                defenders.Add(goblin);

                object task = combatManager.GetType().GetMethod(
                        "StartCombat",
                        new[] { listType, listType, typeof(bool) })
                    .Invoke(combatManager, new object[] { attackers, defenders, true });
                Assert.That(task, Is.Not.Null);
                Assert.That(player.GetType().GetProperty("Stack").GetValue(player), Is.Null);
                Assert.That(goblin.GetType().GetProperty("Stack").GetValue(goblin), Is.Null);

                endAll.Invoke(combatManager, null);

                Assert.That(
                    ((IEnumerable)combatManager.GetType()
                        .GetProperty("ActiveCombats").GetValue(combatManager))
                    .Cast<object>(),
                    Is.Empty);
                Assert.That(player.GetType().GetProperty("Stack").GetValue(player),
                    Is.Not.Null);
                Assert.That(goblin.GetType().GetProperty("Stack").GetValue(goblin),
                    Is.Not.Null);
            }
            finally
            {
                DestroyTestCard(goblin);
                DestroyTestCard(player);
            }
        }

        [Test]
        public void GameData_CreatesEightSlotBackpackAndRepairsMissingOldSaveData()
        {
            System.Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            System.Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            Assert.That(backpackType, Is.Not.Null, "当前正式存档需要独立的全局背包数据");

            object gameData = System.Activator.CreateInstance(gameDataType);
            FieldInfo backpackField = gameDataType.GetField("Backpack");
            MethodInfo ensureBackpack = gameDataType.GetMethod("EnsureBackpack");
            Assert.That(backpackField, Is.Not.Null);
            Assert.That(ensureBackpack, Is.Not.Null);

            object backpack = backpackField.GetValue(gameData);
            Assert.That(backpack, Is.Not.Null);
            Assert.That(backpackType.GetProperty("Capacity").GetValue(backpack), Is.EqualTo(8));

            backpackType.GetField("Entries").SetValue(backpack, null);
            object normalized = ensureBackpack.Invoke(gameData, null);
            Assert.That(backpackType.GetField("Entries").GetValue(normalized), Is.Not.Null,
                "旧存档缺少背包条目列表时必须自动修复");

            backpackField.SetValue(gameData, null);
            object repaired = ensureBackpack.Invoke(gameData, null);
            Assert.That(repaired, Is.Not.Null);
            Assert.That(backpackField.GetValue(gameData), Is.SameAs(repaired));
            Assert.That(backpackType.GetProperty("Capacity").GetValue(repaired), Is.EqualTo(8));
        }

        [Test]
        public void BackpackData_ExpandsWhenFullAndRemovesByStableInstanceId()
        {
            System.Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            object backpack = System.Activator.CreateInstance(backpackType);
            backpackType.GetField("SlotCapacity").SetValue(backpack, 2);
            MethodInfo tryAdd = backpackType.GetMethod("TryAdd");
            MethodInfo tryRemove = backpackType.GetMethod("TryRemove");
            MethodInfo compact = backpackType.GetMethod("Compact");
            Assert.That(tryAdd, Is.Not.Null);
            Assert.That(tryRemove, Is.Not.Null);
            Assert.That(compact, Is.Not.Null);

            object apple = System.Activator.CreateInstance(cardDataType);
            object coin = System.Activator.CreateInstance(cardDataType);
            object sword = System.Activator.CreateInstance(cardDataType);
            cardDataType.GetField("Id").SetValue(apple, "apple");
            cardDataType.GetField("Id").SetValue(coin, "coin");
            cardDataType.GetField("Id").SetValue(sword, "sword");

            object[] firstAdd = { apple, null };
            object[] secondAdd = { coin, null };
            object[] expandedAdd = { sword, null };
            Assert.That(tryAdd.Invoke(backpack, firstAdd), Is.True);
            Assert.That(tryAdd.Invoke(backpack, secondAdd), Is.True);
            Assert.That(tryAdd.Invoke(backpack, expandedAdd), Is.True,
                "当前版本暂不限制背包卡牌上限，格子用满后应自动扩展");
            Assert.That(backpackType.GetProperty("Capacity").GetValue(backpack),
                Is.GreaterThanOrEqualTo(3));

            string firstId = (string)firstAdd[1].GetType().GetField("InstanceId")
                .GetValue(firstAdd[1]);
            Assert.That(firstId, Is.Not.Empty);
            Assert.That(firstAdd[1].GetType().GetField("SlotIndex").GetValue(firstAdd[1]), Is.EqualTo(0));
            Assert.That(secondAdd[1].GetType().GetField("SlotIndex").GetValue(secondAdd[1]), Is.EqualTo(1));

            object[] removeArguments = { firstId, null };
            Assert.That(tryRemove.Invoke(backpack, removeArguments), Is.True);
            Assert.That(removeArguments[1], Is.SameAs(apple));
            Assert.That(backpackType.GetProperty("Count").GetValue(backpack), Is.EqualTo(2));

            compact.Invoke(backpack, null);
            Assert.That(secondAdd[1].GetType().GetField("SlotIndex").GetValue(secondAdd[1]),
                Is.EqualTo(0), "整理背包应把卡牌顺序填入前面的空格");
            Assert.That(expandedAdd[1].GetType().GetField("SlotIndex").GetValue(expandedAdd[1]),
                Is.EqualTo(1));
        }

        [Test]
        public void BackpackData_NormalizeKeepsOverflowedOldSaveEntriesVisible()
        {
            System.Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            System.Type entryType = FindType("CryingSnow.StackCraft.BackpackEntryData");
            System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            object backpack = System.Activator.CreateInstance(backpackType);
            backpackType.GetField("SlotCapacity").SetValue(backpack, 2);
            var entries = (System.Collections.IList)backpackType.GetField("Entries")
                .GetValue(backpack);
            for (int index = 0; index < 3; index++)
            {
                object data = System.Activator.CreateInstance(cardDataType);
                cardDataType.GetField("Id").SetValue(data, $"legacy-{index}");
                object entry = System.Activator.CreateInstance(entryType);
                entryType.GetField("InstanceId").SetValue(entry, $"duplicate-{index}");
                entryType.GetField("Card").SetValue(entry, data);
                entryType.GetField("SlotIndex").SetValue(entry, 0);
                entries.Add(entry);
            }

            backpackType.GetMethod("Normalize").Invoke(backpack, null);
            int capacity = (int)backpackType.GetProperty("Capacity").GetValue(backpack);
            int[] slots = entries.Cast<object>()
                .Select(entry => (int)entryType.GetField("SlotIndex").GetValue(entry))
                .ToArray();
            Assert.That(capacity, Is.GreaterThanOrEqualTo(3),
                "旧存档条目超过原容量时应扩容，不能产生不可见物品");
            Assert.That(slots.Distinct().Count(), Is.EqualTo(3));
            Assert.That(slots.All(slot => slot >= 0 && slot < capacity), Is.True);
        }

        [Test]
        public void BackpackService_AllowsPortableItemsIncludingFoodAndCoins()
        {
            System.Type serviceType = FindType("CryingSnow.StackCraft.BackpackService");
            Assert.That(serviceType, Is.Not.Null);
            MethodInfo canStore = serviceType.GetMethod(
                "CanStoreDefinition",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(canStore, Is.Not.Null);

            string[] allowedPaths =
            {
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset",
                "Assets/StackCraft/Resources/Cards/Currencies/Card_Coin.asset",
                "Assets/StackCraft/Resources/Cards/Materials/Card_Wood.asset",
                "Assets/StackCraft/Resources/Cards/Equipments/Card_Sword.asset"
            };
            foreach (string path in allowedPaths)
            {
                Object definition = AssetDatabase.LoadAssetAtPath<Object>(path);
                Assert.That(definition, Is.Not.Null, path);
                Assert.That(canStore.Invoke(null, new[] { definition }), Is.True, path);
            }

            string[] rejectedPaths =
            {
                "Assets/StackCraft/Resources/Cards/Characters/Card_Villager.asset",
                "Assets/StackCraft/Resources/Cards/Resources/Card_AppleTree.asset",
                "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Inn.asset"
            };
            foreach (string path in rejectedPaths)
            {
                Object definition = AssetDatabase.LoadAssetAtPath<Object>(path);
                Assert.That(definition, Is.Not.Null, path);
                Assert.That(canStore.Invoke(null, new[] { definition }), Is.False, path);
            }
        }

        [Test]
        public void BackpackService_StoresOnePortableWorldCardWithItsRuntimeState()
        {
            System.Type serviceType = FindType("CryingSnow.StackCraft.BackpackService");
            System.Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            System.Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
            MethodInfo tryStore = serviceType.GetMethod(
                "TryStore",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { cardType, backpackType },
                null);
            Assert.That(tryStore, Is.Not.Null);

            Object appleDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset");
            Component apple = CreateUninitializedCard(appleDefinition, "Backpack Apple");
            object backpack = System.Activator.CreateInstance(backpackType);
            try
            {
                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode!"));
                Assert.That(tryStore.Invoke(null, new[] { apple, backpack }), Is.True);
                Assert.That(backpackType.GetProperty("Count").GetValue(backpack), Is.EqualTo(1));
                Assert.That(cardType.GetProperty("Stack").GetValue(apple), Is.Null,
                    "写入背包成功后必须立即退出世界卡堆，避免场景存档重复保存");

                IEnumerable entries = (IEnumerable)backpackType.GetField("Entries").GetValue(backpack);
                object entry = entries.Cast<object>().Single();
                object storedCard = entry.GetType().GetField("Card").GetValue(entry);
                Assert.That(storedCard.GetType().GetField("Id").GetValue(storedCard),
                    Is.EqualTo(appleDefinition.GetType().GetProperty("Id").GetValue(appleDefinition)));
            }
            finally
            {
                DestroyTestCard(apple);
            }
        }

        [Test]
        public void BackpackService_StoresAnEntirePortableCardStackAtomically()
        {
            System.Type serviceType = FindType("CryingSnow.StackCraft.BackpackService");
            System.Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            System.Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
            MethodInfo tryStore = serviceType.GetMethod(
                "TryStore",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { cardType, backpackType },
                null);
            Object appleDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset");
            Component firstApple = CreateUninitializedCard(appleDefinition, "First Backpack Apple");
            Component secondApple = CreateUninitializedCard(appleDefinition, "Second Backpack Apple");
            object backpack = System.Activator.CreateInstance(backpackType);
            try
            {
                for (int index = 0; index < 8; index++)
                {
                    object existing = System.Activator.CreateInstance(cardDataType);
                    cardDataType.GetField("Id").SetValue(existing, $"existing-{index}");
                    backpackType.GetMethod("TryAdd").Invoke(
                        backpack,
                        new object[] { existing, null });
                }
                object firstStack = cardType.GetProperty("Stack").GetValue(firstApple);
                object secondStack = cardType.GetProperty("Stack").GetValue(secondApple);
                firstStack.GetType().GetMethod("MergeWith").Invoke(
                    firstStack,
                    new[] { secondStack });

                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode!"));
                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex("Destroy may not be called from edit mode!"));
                Assert.That(tryStore.Invoke(null, new[] { firstApple, backpack }), Is.True);
                Assert.That(backpackType.GetProperty("Count").GetValue(backpack), Is.EqualTo(10),
                    "玩家拖动堆叠的食物或金币时，整叠应一次进入背包");
                Assert.That(backpackType.GetProperty("Capacity").GetValue(backpack), Is.EqualTo(12),
                    "通过正式收纳服务放入第 9 张卡时也必须自动扩容");
                Assert.That(cardType.GetProperty("Stack").GetValue(firstApple), Is.Null);
                Assert.That(cardType.GetProperty("Stack").GetValue(secondApple), Is.Null);
            }
            finally
            {
                DestroyTestCard(secondApple);
                DestroyTestCard(firstApple);
            }
        }

        [Test]
        public void BackpackService_RemovesStoredCardOnlyAfterWorldRestoreSucceeds()
        {
            System.Type serviceType = FindType("CryingSnow.StackCraft.BackpackService");
            System.Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            System.Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
            System.Type restoreType = typeof(System.Func<,>).MakeGenericType(cardDataType, cardType);
            MethodInfo tryTake = serviceType.GetMethod(
                "TryTake",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { backpackType, typeof(string), restoreType, cardType.MakeByRefType() },
                null);
            Assert.That(tryTake, Is.Not.Null);

            object backpack = System.Activator.CreateInstance(backpackType);
            object cardData = System.Activator.CreateInstance(cardDataType);
            cardDataType.GetField("Id").SetValue(cardData, "apple");
            object[] addArguments = { cardData, null };
            backpackType.GetMethod("TryAdd").Invoke(backpack, addArguments);
            object entry = addArguments[1];
            string instanceId = (string)entry.GetType().GetField("InstanceId").GetValue(entry);

            var dataParameter = System.Linq.Expressions.Expression.Parameter(cardDataType, "data");
            System.Delegate failedRestore = System.Linq.Expressions.Expression.Lambda(
                    restoreType,
                    System.Linq.Expressions.Expression.Constant(null, cardType),
                    dataParameter)
                .Compile();
            object[] failedTake = { backpack, instanceId, failedRestore, null };
            Assert.That(tryTake.Invoke(null, failedTake), Is.False);
            Assert.That(backpackType.GetProperty("Count").GetValue(backpack), Is.EqualTo(1),
                "生成世界卡牌失败时不能丢失背包数据");

            Object appleDefinition = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset");
            Component restoredCard = CreateUninitializedCard(appleDefinition, "Restored Backpack Apple");
            try
            {
                System.Delegate successfulRestore = System.Linq.Expressions.Expression.Lambda(
                        restoreType,
                        System.Linq.Expressions.Expression.Constant(restoredCard, cardType),
                        dataParameter)
                    .Compile();
                object[] successfulTake = { backpack, instanceId, successfulRestore, null };
                Assert.That(tryTake.Invoke(null, successfulTake), Is.True);
                Assert.That(successfulTake[3], Is.SameAs(restoredCard));
                Assert.That(backpackType.GetProperty("Count").GetValue(backpack), Is.Zero);
            }
            finally
            {
                DestroyTestCard(restoredCard);
            }
        }

        [Test]
        public void BackpackService_RemovesThreeDimensionalCardOnlyAfterTransferAccepted()
        {
            System.Type serviceType = FindType("CryingSnow.StackCraft.BackpackService");
            System.Type backpackType = FindType("CryingSnow.StackCraft.BackpackData");
            System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            MethodInfo tryTakeExisting = serviceType.GetMethod(
                "TryTakeExisting",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { backpackType, typeof(string), typeof(System.Func<bool>) },
                null);
            Assert.That(tryTakeExisting, Is.Not.Null,
                "三维背包卡拖回地图时应转移原卡对象，不能销毁后再生成一张替代卡");

            object backpack = System.Activator.CreateInstance(backpackType);
            object cardData = System.Activator.CreateInstance(cardDataType);
            cardDataType.GetField("Id").SetValue(cardData, "egg");
            object[] addArguments = { cardData, null };
            backpackType.GetMethod("TryAdd").Invoke(backpack, addArguments);
            object entry = addArguments[1];
            string instanceId = (string)entry.GetType().GetField("InstanceId").GetValue(entry);

            Assert.That(tryTakeExisting.Invoke(
                null,
                new object[] { backpack, instanceId, new System.Func<bool>(() => false) }),
                Is.False);
            Assert.That(backpackType.GetProperty("Count").GetValue(backpack), Is.EqualTo(1),
                "地图放置失败时三维卡牌仍应属于背包");

            int transfers = 0;
            Assert.That(tryTakeExisting.Invoke(
                null,
                new object[]
                {
                    backpack,
                    instanceId,
                    new System.Func<bool>(() =>
                    {
                        transfers++;
                        return true;
                    })
                }),
                Is.True);
            Assert.That(transfers, Is.EqualTo(1));
            Assert.That(backpackType.GetProperty("Count").GetValue(backpack), Is.Zero);
        }

        [Test]
        public void CardManager_StatsAndOwnedCountsIncludeBackpackFoodAndCoinsButNotCardLimit()
        {
            EditorSceneManager.OpenScene(
                "Assets/StackCraft/Scenes/Location.unity",
                OpenSceneMode.Single);
            System.Type gameDirectorType = FindType("CryingSnow.StackCraft.GameDirector");
            MonoBehaviour gameDirector = (MonoBehaviour)new GameObject("Backpack Stats GameDirector")
                .AddComponent(gameDirectorType);
            gameDirectorType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                .SetValue(null, gameDirector);
            System.Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            object gameData = System.Activator.CreateInstance(gameDataType);
            gameDataType.GetField("GameplayPrefs").SetValue(
                gameData,
                System.Activator.CreateInstance(FindType("CryingSnow.StackCraft.GameplayPrefs")));
            gameDirector.GetType().GetProperty("GameData").SetValue(gameDirector, gameData);

            MonoBehaviour cardManager = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.CardManager");
            System.Type cardManagerType = cardManager.GetType();
            cardManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                .SetValue(null, cardManager);
            cardManagerType.GetMethod(
                    "InitializePrefabLookup",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cardManager, null);
            cardManagerType.GetMethod(
                    "BuildDefinitionDatabase",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cardManager, null);

            Object apple = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset");
            Object coin = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Currencies/Card_Coin.asset");
            System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            object backpack = gameDataType.GetMethod("EnsureBackpack").Invoke(gameData, null);
            MethodInfo tryAdd = backpack.GetType().GetMethod("TryAdd");
            foreach ((Object definition, int nutrition) in new[]
                     {
                         (apple, 7),
                         (coin, 0)
                     })
            {
                object data = System.Activator.CreateInstance(cardDataType);
                cardDataType.GetField("Id").SetValue(
                    data,
                    definition.GetType().GetProperty("Id").GetValue(definition));
                cardDataType.GetField("CurrentNutrition").SetValue(data, nutrition);
                object[] add = { data, null };
                Assert.That(tryAdd.Invoke(backpack, add), Is.True);
            }

            object snapshot = cardManagerType.GetMethod("GetStatsSnapshot")
                .Invoke(cardManager, null);
            Assert.That(snapshot.GetType().GetProperty("TotalNutrition").GetValue(snapshot),
                Is.EqualTo(7), "背包里的食物仍然属于玩家拥有的食物");
            Assert.That(snapshot.GetType().GetProperty("Currency").GetValue(snapshot),
                Is.EqualTo(1), "背包里的金币仍然属于玩家拥有的金币");
            Assert.That(snapshot.GetType().GetProperty("CardsOwned").GetValue(snapshot),
                Is.Zero, "背包物品当前不计入桌面卡牌上限");

            MethodInfo countOwned = cardManagerType.GetMethod("CountOwnedCard");
            Assert.That(countOwned, Is.Not.Null);
            Assert.That(countOwned.Invoke(cardManager, new[] { apple }), Is.EqualTo(1));
            Assert.That(countOwned.Invoke(cardManager, new[] { coin }), Is.EqualTo(1));

            Object.DestroyImmediate(gameDirector.gameObject);
        }

        [Test]
        public void CardManager_RestoringBackpackCardDoesNotEmitNewlyObtainedEvent()
        {
            EditorSceneManager.OpenScene(
                "Assets/StackCraft/Scenes/Location.unity",
                OpenSceneMode.Single);
            System.Type gameDirectorType = FindType("CryingSnow.StackCraft.GameDirector");
            MonoBehaviour gameDirector = (MonoBehaviour)new GameObject("Backpack Restore GameDirector")
                .AddComponent(gameDirectorType);
            gameDirectorType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                .SetValue(null, gameDirector);
            System.Type gameDataType = FindType("CryingSnow.StackCraft.GameData");
            object gameData = System.Activator.CreateInstance(gameDataType);
            gameDataType.GetField("GameplayPrefs").SetValue(
                gameData,
                System.Activator.CreateInstance(FindType("CryingSnow.StackCraft.GameplayPrefs")));
            gameDirector.GetType().GetProperty("GameData").SetValue(gameDirector, gameData);

            MonoBehaviour board = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.Board");
            board.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(board, null);
            MonoBehaviour cardManager = Object.FindObjectsOfType<MonoBehaviour>(true)
                .First(component => component.GetType().FullName ==
                    "CryingSnow.StackCraft.CardManager");
            System.Type cardManagerType = cardManager.GetType();
            cardManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                .SetValue(null, cardManager);
            cardManagerType.GetMethod(
                    "InitializePrefabLookup",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cardManager, null);
            cardManagerType.GetMethod(
                    "BuildDefinitionDatabase",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cardManager, null);

            EventInfo createdEvent = cardManagerType.GetEvent("OnCardCreated");
            System.Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
            var cardParameter = System.Linq.Expressions.Expression.Parameter(cardType, "card");
            System.Delegate handler = System.Linq.Expressions.Expression.Lambda(
                    createdEvent.EventHandlerType,
                    System.Linq.Expressions.Expression.Call(
                        typeof(PlayableLoopUnityTests).GetMethod(
                            nameof(RecordBackpackCreatedEvent),
                            BindingFlags.Static | BindingFlags.NonPublic)),
                    cardParameter)
                .Compile();
            backpackCreatedEventCount = 0;
            createdEvent.AddEventHandler(cardManager, handler);
            EventInfo statsEvent = cardManagerType.GetEvent("OnStatsChanged");
            var statsParameter = System.Linq.Expressions.Expression.Parameter(
                statsEvent.EventHandlerType.GenericTypeArguments[0],
                "stats");
            System.Delegate statsHandler = System.Linq.Expressions.Expression.Lambda(
                    statsEvent.EventHandlerType,
                    System.Linq.Expressions.Expression.Call(
                        typeof(PlayableLoopUnityTests).GetMethod(
                            nameof(RecordBackpackStatsEvent),
                            BindingFlags.Static | BindingFlags.NonPublic)),
                    statsParameter)
                .Compile();
            backpackStatsEventCount = 0;
            statsEvent.AddEventHandler(cardManager, statsHandler);

            Object apple = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset");
            System.Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            object data = System.Activator.CreateInstance(cardDataType);
            cardDataType.GetField("Id").SetValue(
                data,
                apple.GetType().GetProperty("Id").GetValue(apple));
            cardDataType.GetField("UsesLeft").SetValue(data, 1);
            cardDataType.GetField("CurrentNutrition").SetValue(data, 5);

            Component restored = null;
            try
            {
                LogAssert.Expect(
                    LogType.Error,
                    "Instantiating material due to calling renderer.material during edit mode. This will leak materials into the scene. You most likely want to use renderer.sharedMaterial instead.");
                restored = (Component)cardManagerType.GetMethod("RestoreCardFromData")
                    .Invoke(cardManager, new object[] { data, Vector3.zero, false });
                Assert.That(restored, Is.Not.Null);
                Assert.That(backpackCreatedEventCount, Is.Zero,
                    "从背包取回旧卡牌不能再次推进 Obtain/获得卡牌任务");
                Assert.That(backpackStatsEventCount, Is.Zero,
                    "背包数据尚未删除时不能发布临时的双重资源统计");
            }
            finally
            {
                createdEvent.RemoveEventHandler(cardManager, handler);
                statsEvent.RemoveEventHandler(cardManager, statsHandler);
                if (restored != null)
                    Object.DestroyImmediate(restored.gameObject);
                Object.DestroyImmediate(gameDirector.gameObject);
            }
        }

        private static int backpackCreatedEventCount;
        private static int backpackStatsEventCount;

        private static void SetRandomSpawnRule(
            SerializedProperty rule,
            Object definition,
            int minimum,
            int maximum)
        {
            rule.FindPropertyRelative("definition").objectReferenceValue = definition;
            rule.FindPropertyRelative("minimumCount").intValue = minimum;
            rule.FindPropertyRelative("maximumCount").intValue = maximum;
        }

        private static void InitializeTestCombatant(Component card)
        {
            System.Type combatantType = FindType(
                "CryingSnow.StackCraft.CardCombatant");
            Component combatant = card.gameObject.AddComponent(combatantType);
            combatantType.GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(combatant, null);
            card.GetType().GetProperty("Combatant").SetValue(card, combatant);
            card.GetType().GetProperty("Size").SetValue(card, Vector2.one);
        }

        private static int CountRandomSpawnItems(
            IEnumerable<object> items,
            string cardId)
        {
            return items.Count(item =>
            {
                Object definition = (Object)item.GetType()
                    .GetProperty("Definition").GetValue(item);
                return definition.name == cardId;
            });
        }

        private static string CaptureRandomSpawnPlan(IEnumerable<object> items)
        {
            return string.Join(
                "|",
                items.Select(item =>
                {
                    System.Type type = item.GetType();
                    Object definition =
                        (Object)type.GetProperty("Definition").GetValue(item);
                    Vector3 position =
                        (Vector3)type.GetProperty("Position").GetValue(item);
                    return $"{definition.name}@" +
                        $"{position.x:R},{position.z:R}";
                }));
        }

        private static void RecordBackpackCreatedEvent()
        {
            backpackCreatedEventCount++;
        }

        private static void RecordBackpackStatsEvent()
        {
            backpackStatsEventCount++;
        }

        private static string CaptureBackpackLayout(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject uiRoot = GameObject.Find("UIRoot");
            Assert.That(uiRoot, Is.Not.Null, $"{scenePath} 缺少 UIRoot");
            Transform backpackRoot = FindDescendant(uiRoot, "BackpackRoot");
            Assert.That(backpackRoot, Is.Not.Null, $"{scenePath} 缺少 BackpackRoot");

            var builder = new System.Text.StringBuilder();
            foreach (Transform child in backpackRoot
                         .GetComponentsInChildren<Transform>(true)
                         .OrderBy(candidate => GetHierarchyPath(backpackRoot, candidate)))
            {
                builder.Append(GetHierarchyPath(backpackRoot, child))
                    .Append("|active=").Append(child.gameObject.activeSelf);

                if (child is RectTransform rect)
                {
                    builder.Append("|anchors=").Append(Format(rect.anchorMin))
                        .Append(';').Append(Format(rect.anchorMax))
                        .Append("|position=").Append(Format(rect.anchoredPosition))
                        .Append("|size=").Append(Format(rect.sizeDelta))
                        .Append("|pivot=").Append(Format(rect.pivot))
                        .Append("|scale=").Append(Format(rect.localScale));
                }

                Image image = child.GetComponent<Image>();
                if (image != null)
                {
                    builder.Append("|image=").Append(image.enabled)
                        .Append(';').Append(Format(image.color))
                        .Append(';').Append(AssetDatabase.GetAssetPath(image.sprite))
                        .Append(';').Append(image.preserveAspect);
                }

                GridLayoutGroup grid = child.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    builder.Append("|grid=").Append(Format(grid.cellSize))
                        .Append(';').Append(Format(grid.spacing))
                        .Append(';').Append((int)grid.startCorner)
                        .Append(';').Append((int)grid.childAlignment)
                        .Append(';').Append(grid.constraintCount);
                }

                TMPro.TMP_Text text = child.GetComponent<TMPro.TMP_Text>();
                if (text != null)
                {
                    builder.Append("|text=").Append(text.fontSize.ToString("R"))
                        .Append(';').Append(Format(text.color))
                        .Append(';').Append((int)text.alignment);
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static string GetHierarchyPath(Transform root, Transform child)
        {
            var names = new Stack<string>();
            for (Transform current = child;
                 current != null && current != root;
                 current = current.parent)
            {
                names.Push(current.name);
            }
            return names.Count == 0 ? root.name : $"{root.name}/{string.Join("/", names)}";
        }

        private static string Format(Vector2 value) =>
            System.FormattableString.Invariant($"{value.x:R},{value.y:R}");

        private static string Format(Vector3 value) =>
            System.FormattableString.Invariant($"{value.x:R},{value.y:R},{value.z:R}");

        private static string Format(Color value) =>
            System.FormattableString.Invariant(
                $"{value.r:R},{value.g:R},{value.b:R},{value.a:R}");

        private static Transform FindDescendant(GameObject root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == name);
        }

        private static bool HasComponentNamed(GameObject root, string fullTypeName)
        {
            return root != null && root.GetComponentsInChildren<Component>(true)
                .Any(component => component != null && component.GetType().FullName == fullTypeName);
        }

        private static void SetSerializedString(Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
