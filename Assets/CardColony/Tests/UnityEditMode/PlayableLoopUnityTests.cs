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
        public void OriginalUiRoot_HasFixedWorldMapPartyStatusPanelWithoutBackpack()
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
            Assert.That(
                panel.GetComponentsInChildren<Transform>(true).Any(child =>
                    child.name.IndexOf("Backpack", System.StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False,
                "本阶段的小队状态栏不显示背包");

            RectTransform rect = panel.GetComponent<RectTransform>();
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.zero));
            Assert.That(rect.pivot, Is.EqualTo(Vector2.zero));
            Assert.That(rect.sizeDelta.x, Is.GreaterThanOrEqualTo(400f));
            Assert.That(rect.sizeDelta.y, Is.GreaterThanOrEqualTo(300f));
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
        public void MainScene_OnlyRiverbendHasAnImplementedLocalMapInPhaseOne()
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
                bool expected = entry.FindPropertyRelative("locationId").stringValue == "riverbend";
                SerializedProperty implemented = entry.FindPropertyRelative("localMapImplemented");
                Assert.That(implemented, Is.Not.Null,
                    "地点配置必须明确标记是否已有局部地图，避免未完成地点进入空场景");
                Assert.That(implemented.boolValue, Is.EqualTo(expected),
                    "第一阶段应当只开放河湾村的地点地图");
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
            Assert.That(definitions.arraySize, Is.EqualTo(1));
            Object riverbend = definitions.GetArrayElementAtIndex(0).objectReferenceValue;
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

                solverType.GetMethod("ResolveOverlaps").Invoke(
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
    }
}
