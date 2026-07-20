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
            Component card = gameObject.AddComponent(cardType);
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
