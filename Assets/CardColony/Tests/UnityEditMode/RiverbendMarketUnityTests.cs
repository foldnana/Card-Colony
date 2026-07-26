using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CardColony.Tests
{
    public sealed class RiverbendMarketUnityTests
    {
        private const string RiverbendPath =
            "Assets/StackCraft/Resources/Locations/Location_Riverbend.asset";
        private const string MarketPath =
            "Assets/StackCraft/Resources/Locations/Location_RiverbendMarket.asset";
        private const string MarketBackgroundPath =
            "Assets/CardColony/Art/Backgrounds/Market/RiverbendMarketInteriorBackground.png";

        [Test]
        public void RiverbendMarket_IsConfiguredAsEnterableTradingLocation()
        {
            UnityEngine.Object riverbend =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(RiverbendPath);
            UnityEngine.Object market =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MarketPath);

            Assert.That(riverbend, Is.Not.Null);
            Assert.That(market, Is.Not.Null,
                "河湾市场需要独立的 LocationDefinition。");

            var marketSerialized = new SerializedObject(market);
            Assert.That(marketSerialized.FindProperty("id").stringValue,
                Is.EqualTo("riverbend-market"));
            UnityEngine.Object configuredMarketCurrency =
                marketSerialized.FindProperty("marketCurrencyCardDefinition")
                    ?.objectReferenceValue;
            UnityEngine.Object expectedMarketCurrency =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Currencies/Card_Coin.asset");
            Assert.That(configuredMarketCurrency, Is.EqualTo(expectedMarketCurrency),
                "河湾市场应在地点配置中独立指定金币。");
            Assert.That(
                marketSerialized.FindProperty("backgroundTexture")
                    .objectReferenceValue,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<Texture2D>(
                    MarketBackgroundPath)));

            var riverbendSerialized = new SerializedObject(riverbend);
            SerializedProperty entrances =
                riverbendSerialized.FindProperty("entrances");
            bool hasMarketEntrance = false;
            for (int index = 0; index < entrances.arraySize; index++)
            {
                SerializedProperty entrance =
                    entrances.GetArrayElementAtIndex(index);
                UnityEngine.Object source = entrance
                    .FindPropertyRelative("sourceCardDefinition")
                    .objectReferenceValue;
                string sourceId = source == null
                    ? string.Empty
                    : new SerializedObject(source).FindProperty("id").stringValue;
                if (sourceId == "riverbend-market" &&
                    entrance.FindPropertyRelative("destinationLocationId")
                        .stringValue == "riverbend-market")
                {
                    hasMarketEntrance = true;
                    break;
                }
            }
            Assert.That(hasMarketEntrance, Is.True,
                "河湾村的市场建筑卡需要连接到市场内部地点。");

            SerializedProperty buyer =
                marketSerialized.FindProperty("marketBuyerCardDefinition");
            SerializedProperty pickup =
                marketSerialized.FindProperty("marketPickupCardDefinition");
            SerializedProperty offers =
                marketSerialized.FindProperty("marketOffers");
            Assert.That(buyer, Is.Not.Null,
                "通用地点定义需要能够配置市场收购台。");
            Assert.That(buyer.objectReferenceValue, Is.Not.Null);
            Assert.That(pickup, Is.Not.Null,
                "市场需要配置购买后商品出现的取货台。");
            Assert.That(pickup.objectReferenceValue, Is.Not.Null);
            SerializedProperty initialSpawns =
                marketSerialized.FindProperty("initialCardSpawns");
            Vector3 pickupPosition = Enumerable
                .Range(0, initialSpawns.arraySize)
                .Select(index => initialSpawns.GetArrayElementAtIndex(index))
                .Where(spawn =>
                    spawn.FindPropertyRelative("definition")
                        .objectReferenceValue == pickup.objectReferenceValue)
                .Select(spawn =>
                    spawn.FindPropertyRelative("position").vector3Value)
                .Single();
            Assert.That(pickupPosition,
                Is.EqualTo(new Vector3(4.6f, 0f, -2.65f)),
                "取货台应位于右下区域内部，不能贴近右侧 UI 遮挡区。");
            Assert.That(offers, Is.Not.Null,
                "通用地点定义需要能够配置带每日库存的商品摊位。");
            Assert.That(offers.arraySize, Is.EqualTo(7));

            UnityEngine.Object[] expectedProducts =
            {
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Berry.asset"),
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset"),
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Potato.asset"),
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_RawMeat.asset"),
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Materials/Card_Wood.asset"),
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Materials/Card_Stone.asset"),
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Materials/Card_Rope.asset")
            };
            UnityEngine.Object[] configuredProducts =
                Enumerable.Range(0, offers.arraySize)
                .Select(index => offers.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("productDefinition")
                    .objectReferenceValue)
                .Where(definition => definition != null)
                .ToArray();
            Assert.That(configuredProducts,
                Is.EquivalentTo(expectedProducts));

            for (int index = 0; index < offers.arraySize; index++)
            {
                SerializedProperty offer = offers.GetArrayElementAtIndex(index);
                SerializedProperty minimumStock =
                    offer.FindPropertyRelative("minimumDailyStock");
                SerializedProperty maximumStock =
                    offer.FindPropertyRelative("maximumDailyStock");
                Assert.That(minimumStock, Is.Not.Null);
                Assert.That(maximumStock, Is.Not.Null);
                Assert.That(
                    minimumStock.intValue,
                    Is.GreaterThanOrEqualTo(1));
                Assert.That(
                    maximumStock.intValue,
                    Is.GreaterThanOrEqualTo(minimumStock.intValue));
            }
        }

        [Test]
        public void WorldMapLocationView_ExposesMarketOfferAndBuyerSelections()
        {
            Type viewType =
                FindType("CryingSnow.StackCraft.WorldMapLocationView");
            Type vendorType =
                FindType("CryingSnow.StackCraft.MarketProductVendor");
            Type buyerType =
                FindType("CryingSnow.StackCraft.MarketCardBuyer");
            Assert.That(viewType.GetProperty("SelectedMarketOffer"), Is.Not.Null,
                "右侧地点面板需要能够切换为商品详情。");
            Assert.That(viewType.GetProperty("SelectedMarketBuyer"), Is.Not.Null,
                "右侧地点面板需要能够切换为收购确认。");
            Assert.That(
                viewType.GetMethod("ShowMarketOffer", new[] { vendorType }),
                Is.Not.Null);
            Assert.That(
                viewType.GetMethod("ShowMarketBuyer", new[] { buyerType }),
                Is.Not.Null);
            Assert.That(
                viewType.GetMethod(
                    "HandleMarketFundsChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "金币从背包或桌面变化后，右侧购买按钮需要立即刷新。");
        }

        [Test]
        public void WorldMapLocationView_ShowMarketOffer_ReopensHiddenProductBody()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/StackCraft/Scenes/Location.unity",
                OpenSceneMode.Additive);
            GameObject vendorObject = new("Market Offer View Test");
            try
            {
                Type viewType =
                    FindType("CryingSnow.StackCraft.WorldMapLocationView");
                Type vendorType =
                    FindType("CryingSnow.StackCraft.MarketProductVendor");
                Component view = scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<Component>(true))
                    .FirstOrDefault(component =>
                        component != null &&
                        component.GetType() == viewType);
                Assert.That(view, Is.Not.Null);

                Toggle locationToggle = viewType.GetField(
                        "locationToggle",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(view) as Toggle;
                Assert.That(locationToggle, Is.Not.Null);
                locationToggle.isOn = true;

                CanvasGroup canvasGroup = view.GetComponent<CanvasGroup>();
                viewType.GetMethod("ToggleView")
                    ?.Invoke(view, new object[] { false });
                Assert.That(canvasGroup.alpha, Is.Zero);

                UnityEngine.Object currency =
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                        "Assets/StackCraft/Resources/Cards/Currencies/Card_Coin.asset");
                UnityEngine.Object product =
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                        "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset");
                Component vendor = vendorObject.AddComponent(vendorType);
                vendorType.GetMethods()
                    .Single(method =>
                        method.Name == "Configure" &&
                        method.GetParameters().Length == 3)
                    .Invoke(vendor, new[] { product, (object)3, currency });

                viewType.GetMethod("ShowMarketOffer", new[] { vendorType })
                    ?.Invoke(view, new[] { vendor });

                Assert.That(canvasGroup.alpha, Is.EqualTo(1f),
                    "商品标签已处于选中状态时，显示新商品也必须重新显示右侧正文。");
                Assert.That(canvasGroup.interactable, Is.True);
                Assert.That(canvasGroup.blocksRaycasts, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(vendorObject);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void WorldMapLocationView_ShowMarketBuyer_ReopensHiddenProductBody()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/StackCraft/Scenes/Location.unity",
                OpenSceneMode.Additive);
            GameObject buyerObject = new("Market Buyer View Test");
            try
            {
                Type viewType =
                    FindType("CryingSnow.StackCraft.WorldMapLocationView");
                Type buyerType =
                    FindType("CryingSnow.StackCraft.MarketCardBuyer");
                Component view = scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<Component>(true))
                    .FirstOrDefault(component =>
                        component != null &&
                        component.GetType() == viewType);
                Assert.That(view, Is.Not.Null);

                Toggle locationToggle = viewType.GetField(
                        "locationToggle",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(view) as Toggle;
                Assert.That(locationToggle, Is.Not.Null);
                locationToggle.isOn = true;

                CanvasGroup canvasGroup = view.GetComponent<CanvasGroup>();
                viewType.GetMethod("ToggleView")
                    ?.Invoke(view, new object[] { false });
                Assert.That(canvasGroup.alpha, Is.Zero);

                UnityEngine.Object currency =
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                        "Assets/StackCraft/Resources/Cards/Currencies/Card_Coin.asset");
                Component buyer = buyerObject.AddComponent(buyerType);
                buyerType.GetMethod("Configure")
                    ?.Invoke(buyer, new[] { currency });

                viewType.GetMethod("ShowMarketBuyer", new[] { buyerType })
                    ?.Invoke(view, new[] { buyer });

                Assert.That(canvasGroup.alpha, Is.EqualTo(1f),
                    "从商品切换到收购台时，右侧收购正文必须重新显示。");
                Assert.That(canvasGroup.interactable, Is.True);
                Assert.That(canvasGroup.blocksRaycasts, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(buyerObject);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void MarketStockLedger_PreservesSameDayStockAndRefreshesNextDay()
        {
            Type ledgerType =
                FindType("CryingSnow.StackCraft.MarketStockLedger");
            Type sceneDataType =
                FindType("CryingSnow.StackCraft.SceneData");
            Assert.That(ledgerType, Is.Not.Null,
                "每日市场库存账本尚未实现。");

            object sceneData = Activator.CreateInstance(sceneDataType);
            MethodInfo getOrRefresh = ledgerType.GetMethod(
                "GetOrRefresh",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo tryConsume = ledgerType.GetMethod(
                "TryConsume",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(getOrRefresh, Is.Not.Null);
            Assert.That(tryConsume, Is.Not.Null);

            object firstDay = getOrRefresh.Invoke(
                null,
                new[] { sceneData, "apple-offer", (object)1, 2, 4 });
            int initialStock = (int)firstDay.GetType()
                .GetProperty("Remaining").GetValue(firstDay);
            Assert.That(initialStock, Is.InRange(2, 4));

            Assert.That(tryConsume.Invoke(
                null,
                new[] { sceneData, "apple-offer", (object)1, 2, 4 }),
                Is.True);
            object sameDay = getOrRefresh.Invoke(
                null,
                new[] { sceneData, "apple-offer", (object)1, 2, 4 });
            Assert.That(
                sameDay.GetType().GetProperty("Remaining").GetValue(sameDay),
                Is.EqualTo(initialStock - 1),
                "同一天离开再进入市场不能补满库存。");

            object nextDay = getOrRefresh.Invoke(
                null,
                new[] { sceneData, "apple-offer", (object)2, 2, 4 });
            Assert.That(
                nextDay.GetType().GetProperty("Day").GetValue(nextDay),
                Is.EqualTo(2));
            Assert.That(
                (int)nextDay.GetType().GetProperty("Remaining").GetValue(nextDay),
                Is.InRange(2, 4));
        }

        [Test]
        public void GameData_UsesOneWorldDayAcrossLocationScopes()
        {
            Type gameDataType =
                FindType("CryingSnow.StackCraft.GameData");
            object gameData = Activator.CreateInstance(gameDataType);
            MethodInfo setWorldDay = gameDataType.GetMethod("SetWorldDay");
            MethodInfo getWorldDay = gameDataType.GetMethod("GetWorldDay");
            Assert.That(setWorldDay, Is.Not.Null);
            Assert.That(getWorldDay, Is.Not.Null,
                "市场库存需要读取跨地点共享的世界日期。");

            setWorldDay.Invoke(gameData, new object[] { 5 });
            gameDataType.GetField("ActiveLocationId")
                .SetValue(gameData, "riverbend-market");
            Assert.That(getWorldDay.Invoke(gameData, new object[] { 1 }),
                Is.EqualTo(5));

            gameDataType.GetField("ActiveLocationId")
                .SetValue(gameData, "riverbend");
            Assert.That(getWorldDay.Invoke(gameData, new object[] { 1 }),
                Is.EqualTo(5));
        }

        [Test]
        public void MarketCurrencyService_SpendsLooseCoinsFromBackpackAndTable()
        {
            Type serviceType =
                FindType("CryingSnow.StackCraft.MarketCurrencyService");
            Type backpackType =
                FindType("CryingSnow.StackCraft.BackpackData");
            Type cardDataType =
                FindType("CryingSnow.StackCraft.CardData");
            Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
            Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            Assert.That(serviceType, Is.Not.Null,
                "市场从背包与桌面统一扣款的服务尚未实现。");

            UnityEngine.Object currency =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Currencies/Card_Coin.asset");
            object backpack = Activator.CreateInstance(backpackType);
            object storedCoin = Activator.CreateInstance(cardDataType);
            cardDataType.GetField("Id").SetValue(
                storedCoin,
                new SerializedObject(currency).FindProperty("id").stringValue);
            object[] addArguments = { storedCoin, null };
            Assert.That(
                backpackType.GetMethod("TryAdd").Invoke(backpack, addArguments),
                Is.True);

            GameObject worldCoinObject = new("Market Table Coin");
            try
            {
                Component worldCoin = worldCoinObject.AddComponent(cardType);
                SetDefinition(worldCoin, currency);
                object worldStack = Activator.CreateInstance(
                    stackType,
                    worldCoin,
                    Vector3.zero);
                cardType.GetProperty("Stack").SetValue(worldCoin, worldStack);

                Array worldCards = CreateTypedArray(cardType, worldCoin);
                MethodInfo countAvailable = serviceType.GetMethod(
                    "CountAvailable",
                    BindingFlags.Public | BindingFlags.Static);
                MethodInfo trySpend = serviceType.GetMethod(
                    "TrySpend",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.That(
                    countAvailable.Invoke(
                        null,
                        new[] { currency, backpack, worldCards }),
                    Is.EqualTo(2));
                LogAssert.Expect(
                    LogType.Error,
                    new System.Text.RegularExpressions.Regex(
                        "Destroy may not be called from edit mode"));
                Assert.That(
                    trySpend.Invoke(
                        null,
                        new[] { currency, (object)2, backpack, worldCards }),
                    Is.True);
                Assert.That(
                    backpackType.GetProperty("Count").GetValue(backpack),
                    Is.EqualTo(0));
                Assert.That(
                    ((System.Collections.ICollection)stackType
                        .GetProperty("Cards").GetValue(worldStack)).Count,
                    Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldCoinObject);
            }
        }

        [Test]
        public void BackpackService_StoresGeneratedCoinsAsOneVisualStackAndRefreshesOnce()
        {
            Type serviceType =
                FindType("CryingSnow.StackCraft.BackpackService");
            Type backpackType =
                FindType("CryingSnow.StackCraft.BackpackData");
            MethodInfo storeGenerated = serviceType.GetMethod(
                "TryStoreGeneratedCards",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(storeGenerated, Is.Not.Null,
                "出售奖励需要一次性写入背包，避免逐枚金币反复重建背包画面。");

            UnityEngine.Object currency =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Currencies/Card_Coin.asset");
            object backpack = Activator.CreateInstance(backpackType);
            int refreshCount = 0;
            Action changed = () => refreshCount++;
            EventInfo changedEvent = serviceType.GetEvent(
                "Changed",
                BindingFlags.Public | BindingFlags.Static);
            changedEvent.AddEventHandler(null, changed);
            try
            {
                Assert.That(
                    storeGenerated.Invoke(
                        null,
                        new[] { currency, (object)3, backpack }),
                    Is.True);

                var entries = ((System.Collections.IEnumerable)backpackType
                        .GetField("Entries").GetValue(backpack))
                    .Cast<object>()
                    .ToArray();
                Assert.That(entries, Has.Length.EqualTo(3));
                Assert.That(refreshCount, Is.EqualTo(1),
                    "一笔出售只能触发一次背包画面重建。");

                string[] stackIds = entries
                    .Select(entry => (string)entry.GetType()
                        .GetField("TableStackId").GetValue(entry))
                    .ToArray();
                Assert.That(stackIds.All(id =>
                    !string.IsNullOrWhiteSpace(id)), Is.True);
                Assert.That(stackIds.Distinct().Count(), Is.EqualTo(1),
                    "同一笔出售获得的金币应在背包桌面显示为一个卡堆。");
                Assert.That(
                    entries.Select(entry => (int)entry.GetType()
                            .GetField("TableStackOrder").GetValue(entry))
                        .OrderBy(order => order),
                    Is.EqualTo(new[] { 0, 1, 2 }));
            }
            finally
            {
                changedEvent.RemoveEventHandler(null, changed);
            }
        }

        [Test]
        public void MarketTradeRules_OnlySellPortableGoodsWithPositiveValue()
        {
            Type rulesType = FindType("CryingSnow.StackCraft.MarketTradeRules");
            Assert.That(rulesType, Is.Not.Null,
                "市场交易规则尚未实现。");
            MethodInfo canSell = rulesType.GetMethod(
                "CanSell",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(canSell, Is.Not.Null);

            ScriptableObject food = CreateDefinition(
                "test-food",
                categoryIndex: 3,
                sellable: true,
                sellPrice: 2,
                isLocationStatic: false);
            ScriptableObject staticMaterial = CreateDefinition(
                "test-static-material",
                categoryIndex: 4,
                sellable: true,
                sellPrice: 2,
                isLocationStatic: true);
            ScriptableObject character = CreateDefinition(
                "test-character",
                categoryIndex: 2,
                sellable: true,
                sellPrice: 2,
                isLocationStatic: false);
            ScriptableObject freeMaterial = CreateDefinition(
                "test-free-material",
                categoryIndex: 4,
                sellable: true,
                sellPrice: 0,
                isLocationStatic: false);

            try
            {
                Assert.That(canSell.Invoke(null, new object[] { food }), Is.True);
                Assert.That(canSell.Invoke(null, new object[] { staticMaterial }), Is.False);
                Assert.That(canSell.Invoke(null, new object[] { character }), Is.False);
                Assert.That(canSell.Invoke(null, new object[] { freeMaterial }), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(food);
                UnityEngine.Object.DestroyImmediate(staticMaterial);
                UnityEngine.Object.DestroyImmediate(character);
                UnityEngine.Object.DestroyImmediate(freeMaterial);
            }
        }

        [Test]
        public void MarketProductVendor_UsesSelectionInsteadOfDraggedPayment()
        {
            Type vendorType =
                FindType("CryingSnow.StackCraft.MarketProductVendor");
            Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
            Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            Assert.That(vendorType, Is.Not.Null,
                "固定价商品摊位尚未实现。");

            UnityEngine.Object currency =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Currencies/Card_Coin.asset");
            UnityEngine.Object product =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Berry.asset");
            Assert.That(currency, Is.Not.Null);
            Assert.That(product, Is.Not.Null);

            GameObject vendorObject = new("Market Vendor Test");
            GameObject firstCoinObject = new("Coin A");
            GameObject secondCoinObject = new("Coin B");
            GameObject thirdCoinObject = new("Coin C");
            try
            {
                Component vendor = vendorObject.AddComponent(vendorType);
                vendorType.GetMethods()
                    .Single(method =>
                        method.Name == "Configure" &&
                        method.GetParameters().Length == 3)
                    .Invoke(
                    vendor,
                    new[] { product, (object)2, currency });
                Assert.DoesNotThrow(() =>
                    vendorType.GetMethod("PlayPuffParticle")?.Invoke(vendor, null),
                    "运行时挂接的市场摊位没有粒子预制体时也必须能够交易。");

                Component firstCoin = firstCoinObject.AddComponent(cardType);
                Component secondCoin = secondCoinObject.AddComponent(cardType);
                Component thirdCoin = thirdCoinObject.AddComponent(cardType);
                SetDefinition(firstCoin, currency);
                SetDefinition(secondCoin, currency);
                SetDefinition(thirdCoin, currency);
                object payment = Activator.CreateInstance(
                    stackType,
                    firstCoin,
                    Vector3.zero);
                stackType.GetMethod("AddCard").Invoke(
                    payment,
                    new object[] { secondCoin });

                MethodInfo canTrade = vendorType.GetMethod("CanTrade");
                Assert.That(canTrade, Is.Not.Null);
                Assert.That(canTrade.Invoke(vendor, new[] { payment }), Is.False,
                    "新购买流程不能再通过把金币拖到商品卡上触发。");
                Assert.That(vendorType.GetMethod("TryPurchase"), Is.Not.Null,
                    "商品摊位需要向右侧详情面板提供购买操作。");
                object deliveryStackToIgnore = vendorType
                    .GetProperty(
                        "DeliveryStackToIgnore",
                        BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null);
                object refuseAll = stackType
                    .GetField(
                        "RefuseAll",
                        BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null);
                Assert.That(deliveryStackToIgnore, Is.SameAs(refuseAll),
                    "取货台生成的商品不应自动并入附近卡堆或触发合成。");
                Assert.That(vendorType.GetProperty("StockRemaining"), Is.Not.Null,
                    "商品摊位需要向右侧详情面板公开当前库存。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(vendorObject);
                UnityEngine.Object.DestroyImmediate(firstCoinObject);
                UnityEngine.Object.DestroyImmediate(secondCoinObject);
                UnityEngine.Object.DestroyImmediate(thirdCoinObject);
            }
        }

        [Test]
        public void MarketCardBuyer_WaitsForConfirmationBeforeConsumingItem()
        {
            Type buyerType =
                FindType("CryingSnow.StackCraft.MarketCardBuyer");
            Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
            Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            UnityEngine.Object currency =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Currencies/Card_Coin.asset");
            UnityEngine.Object product =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset");

            GameObject buyerObject = new("Market Buyer Confirmation Test");
            GameObject productObject = new("Sellable Apple");
            GameObject eventSystemObject = new("Market Buyer Event System");
            try
            {
                Component buyer = buyerObject.AddComponent(buyerType);
                buyerType.GetMethod("Configure")?.Invoke(
                    buyer,
                    new[] { currency });
                Component productCard = productObject.AddComponent(cardType);
                SetDefinition(productCard, product);
                object stack = Activator.CreateInstance(
                    stackType,
                    productCard,
                    Vector3.zero);
                cardType.GetProperty("Stack").SetValue(productCard, stack);

                bool handled = (bool)buyerType.GetMethod(
                        "TryTradeAndConsumeStack")
                    .Invoke(buyer, new[] { stack });
                Assert.That(handled, Is.True);
                Assert.That(
                    ((System.Collections.ICollection)stackType
                        .GetProperty("Cards").GetValue(stack)).Count,
                    Is.EqualTo(1),
                    "放上收购台后必须等待玩家确认，不能立即销毁物品。");
                Assert.That(
                    buyerType.GetProperty("PendingSellValue")?.GetValue(buyer),
                    Is.EqualTo(2));
                var eventSystem = eventSystemObject.AddComponent<
                    UnityEngine.EventSystems.EventSystem>();
                var click = new UnityEngine.EventSystems.PointerEventData(
                    eventSystem)
                {
                    button = UnityEngine.EventSystems.PointerEventData
                        .InputButton.Left
                };
                buyerType.GetMethod("OnPointerClick")
                    ?.Invoke(buyer, new object[] { click });
                Assert.That(
                    buyerType.GetProperty("PendingStack")?.GetValue(buyer),
                    Is.Null,
                    "关闭收购详情时也必须取消本次待售状态。");

                buyerType.GetMethod("TryTradeAndConsumeStack")
                    ?.Invoke(buyer, new[] { stack });
                buyerType.GetMethod(
                        "NotifyCardClicked",
                        BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new[] { productCard });
                Assert.That(
                    buyerType.GetProperty("PendingStack")?.GetValue(buyer),
                    Is.Null,
                    "拿起待售物准备拖离柜台时，应自动取消出售预览。");
                Assert.That(buyerType.GetMethod("ConfirmSale"), Is.Not.Null);
                Assert.That(buyerType.GetMethod("CancelPendingSale"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(buyerObject);
                UnityEngine.Object.DestroyImmediate(productObject);
                UnityEngine.Object.DestroyImmediate(eventSystemObject);
            }
        }

        [Test]
        public void MarketCardBuyer_MergesAdditionalPendingCardsIntoOneStack()
        {
            Type buyerType =
                FindType("CryingSnow.StackCraft.MarketCardBuyer");
            Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
            Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            Type controllerType =
                FindType("CryingSnow.StackCraft.CardController");
            UnityEngine.Object currency =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Currencies/Card_Coin.asset");
            UnityEngine.Object product =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset");
            UnityEngine.Object differentProduct =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Materials/Card_Wood.asset");

            GameObject buyerObject = new("Market Buyer Stack Test");
            GameObject firstProductObject = new("First Sellable Apple");
            GameObject secondProductObject = new("Second Sellable Apple");
            try
            {
                buyerObject.transform.position = new Vector3(0f, 0f, 2.75f);
                Component buyer = buyerObject.AddComponent(buyerType);
                buyerType.GetMethod("Configure")
                    ?.Invoke(buyer, new[] { currency });

                firstProductObject.AddComponent<MeshFilter>();
                secondProductObject.AddComponent<MeshFilter>();
                Component firstCard =
                    firstProductObject.AddComponent(cardType);
                Component secondCard =
                    secondProductObject.AddComponent(cardType);
                SetDefinition(firstCard, product);
                SetDefinition(secondCard, differentProduct);
                object firstStack = Activator.CreateInstance(
                    stackType,
                    firstCard,
                    Vector3.zero);
                object secondStack = Activator.CreateInstance(
                    stackType,
                    secondCard,
                    Vector3.right);
                cardType.GetProperty("Stack")
                    ?.SetValue(firstCard, firstStack);
                cardType.GetProperty("Stack")
                    ?.SetValue(secondCard, secondStack);

                MethodInfo trade = buyerType.GetMethod(
                    "TryTradeAndConsumeStack");
                Assert.That(trade.Invoke(buyer, new[] { firstStack }), Is.True);

                MethodInfo findDropTarget = buyerType.GetMethod(
                    "TryFindDropTarget",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.That(findDropTarget, Is.Not.Null,
                    "收购台需要把待售卡堆所在位置也作为有效投放区域，而不能只检测收购台卡牌本体。");
                object[] findArguments =
                {
                    new Vector3(-1.4f, 0f, 2.5f),
                    0.25f,
                    null
                };
                Assert.That(
                    findDropTarget.Invoke(null, findArguments),
                    Is.True);
                Assert.That(findArguments[2], Is.SameAs(buyer));

                buyerType.GetMethod(
                        "NotifyCardClicked",
                        BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new[] { secondCard });
                Assert.That(
                    buyerType.GetProperty("PendingStack")?.GetValue(buyer),
                    Is.SameAs(firstStack),
                    "拿起新的可售卡牌时应保留当前待售卡堆，以便继续放入收购台。");
                secondCard.transform.position =
                    new Vector3(-1.4f, 0f, 2.5f);
                Component controller =
                    secondProductObject.AddComponent(controllerType);
                controllerType.GetField(
                        "_card",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(controller, secondCard);
                Assert.That(
                    controllerType.GetMethod(
                            "TryTradeWithNearbyZone",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(controller, null),
                    Is.True,
                    "真实放下流程必须优先识别待售区域，不能退回普通异类卡牌堆叠。");

                object pending = buyerType.GetProperty("PendingStack")
                    ?.GetValue(buyer);
                Assert.That(pending, Is.SameAs(firstStack),
                    "追加待售卡牌时必须保留原待售卡堆作为锚点。");
                Assert.That(
                    ((System.Collections.ICollection)stackType
                        .GetProperty("Cards").GetValue(firstStack)).Count,
                    Is.EqualTo(2),
                    "不同类型的可售卡牌也应统一进入待售卡堆，不能被普通卡牌堆叠规则拒绝。");
                Assert.That(
                    ((System.Collections.ICollection)stackType
                        .GetProperty("Cards").GetValue(secondStack)).Count,
                    Is.Zero);
                object[] stackedAreaArguments =
                {
                    new Vector3(-1.4f, 0f, 2.1f),
                    0.25f,
                    null
                };
                Assert.That(
                    findDropTarget.Invoke(null, stackedAreaArguments),
                    Is.True,
                    "待售堆变高后，拖到可见尾部卡牌附近仍应属于收购区域。");
                Assert.That(stackedAreaArguments[2], Is.SameAs(buyer));
                firstCard.transform.position =
                    new Vector3(2f, 0f, 1f);
                object[] movedCardArguments =
                {
                    firstCard.transform.position,
                    0.25f,
                    null
                };
                Assert.That(
                    findDropTarget.Invoke(null, movedCardArguments),
                    Is.True,
                    "待售堆仍在移动时，当前可见卡牌位置也必须能接收投放。");
                Assert.That(movedCardArguments[2], Is.SameAs(buyer));
                Assert.That(
                    stackType.GetProperty("TargetPosition")
                        .GetValue(firstStack),
                    Is.EqualTo(new Vector3(-1.4f, 0f, 2.5f)),
                    "待售卡堆应落在收购柜台左侧台面，不能与中央杂货商重叠。");
                Assert.That(
                    firstCard.transform.Find("Highlight")?.gameObject.activeSelf,
                    Is.True,
                    "待售卡牌必须显示醒目的待售外框。");
                Assert.That(
                    secondCard.transform.Find("Highlight")?.gameObject.activeSelf,
                    Is.True);
                Assert.That(
                    ((ValueTuple<string, string>)buyerType
                        .GetMethod("GetInfo").Invoke(buyer, null)).Item2,
                    Does.Contain("待售 2"));

                buyerType.GetMethod("CancelPendingSale")
                    ?.Invoke(buyer, null);
                Assert.That(
                    firstCard.transform.Find("Highlight")?.gameObject.activeSelf,
                    Is.False,
                    "取消待售后必须清除卡牌外框提示。");
                Assert.That(
                    secondCard.transform.Find("Highlight")?.gameObject.activeSelf,
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(buyerObject);
                UnityEngine.Object.DestroyImmediate(firstProductObject);
                UnityEngine.Object.DestroyImmediate(secondProductObject);
            }
        }

        [Test]
        public void MarketCardBuyer_RefreshesBackpackAfterSoldCardsAreRemoved()
        {
            EditorSceneManager.OpenScene(
                "Assets/StackCraft/Scenes/Location.unity",
                OpenSceneMode.Single);
            Type gameDirectorType =
                FindType("CryingSnow.StackCraft.GameDirector");
            Type gameDataType =
                FindType("CryingSnow.StackCraft.GameData");
            Type cardManagerType =
                FindType("CryingSnow.StackCraft.CardManager");
            Type buyerType =
                FindType("CryingSnow.StackCraft.MarketCardBuyer");
            Type cardType =
                FindType("CryingSnow.StackCraft.CardInstance");
            Type stackType =
                FindType("CryingSnow.StackCraft.CardStack");
            Type backpackServiceType =
                FindType("CryingSnow.StackCraft.BackpackService");

            GameObject directorObject = new("Market Sale Order GameDirector");
            GameObject buyerObject = new("Market Sale Order Buyer");
            GameObject productObject = new("Market Sale Order Apple");
            Action changed = null;
            EventInfo changedEvent = backpackServiceType.GetEvent(
                "Changed",
                BindingFlags.Public | BindingFlags.Static);
            Component director = null;
            try
            {
                director = directorObject.AddComponent(gameDirectorType);
                gameDirectorType.GetProperty(
                        "Instance",
                        BindingFlags.Public | BindingFlags.Static)
                    ?.SetValue(null, director);
                object gameData = Activator.CreateInstance(gameDataType);
                gameDataType.GetField("GameplayPrefs")?.SetValue(
                    gameData,
                    Activator.CreateInstance(
                        FindType("CryingSnow.StackCraft.GameplayPrefs")));
                gameDirectorType.GetProperty("GameData")
                    ?.SetValue(director, gameData);

                Component cardManager =
                    UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true)
                        .First(component => component.GetType() ==
                            cardManagerType);
                cardManagerType.GetProperty(
                        "Instance",
                        BindingFlags.Public | BindingFlags.Static)
                    ?.SetValue(null, cardManager);

                UnityEngine.Object currency =
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                        "Assets/StackCraft/Resources/Cards/Currencies/Card_Coin.asset");
                UnityEngine.Object product =
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                        "Assets/StackCraft/Resources/Cards/Consumables/Card_Apple.asset");
                Component buyer = buyerObject.AddComponent(buyerType);
                buyerType.GetMethod("Configure")
                    ?.Invoke(buyer, new[] { currency });
                Component productCard = productObject.AddComponent(cardType);
                SetDefinition(productCard, product);
                object soldStack = Activator.CreateInstance(
                    stackType,
                    productCard,
                    Vector3.zero);
                cardType.GetProperty("Stack")
                    ?.SetValue(productCard, soldStack);
                cardManagerType.GetMethod("RegisterStack")
                    ?.Invoke(cardManager, new[] { soldStack });
                Assert.That(
                    buyerType.GetMethod("TryTradeAndConsumeStack")
                        ?.Invoke(buyer, new[] { soldStack }),
                    Is.True);

                int refreshCount = 0;
                int soldCardsSeenDuringRefresh = -1;
                changed = () =>
                {
                    refreshCount++;
                    soldCardsSeenDuringRefresh =
                        ((System.Collections.ICollection)stackType
                            .GetProperty("Cards").GetValue(soldStack)).Count;
                };
                changedEvent.AddEventHandler(null, changed);

                LogAssert.Expect(
                    LogType.Error,
                    "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");
                Assert.That(
                    buyerType.GetMethod("ConfirmSale")?.Invoke(buyer, null),
                    Is.True);
                Assert.That(refreshCount, Is.EqualTo(1),
                    "出售成功后背包只能重建一次。");
                Assert.That(soldCardsSeenDuringRefresh, Is.Zero,
                    "背包与顶部统计刷新时，已售卡牌必须先从世界中移除。");
            }
            finally
            {
                if (changed != null)
                    changedEvent.RemoveEventHandler(null, changed);
                gameDirectorType.GetProperty(
                        "Instance",
                        BindingFlags.Public | BindingFlags.Static)
                    ?.SetValue(null, null);
                cardManagerType.GetProperty(
                        "Instance",
                        BindingFlags.Public | BindingFlags.Static)
                    ?.SetValue(null, null);
                UnityEngine.Object.DestroyImmediate(buyerObject);
                if (productObject != null)
                    UnityEngine.Object.DestroyImmediate(productObject);
                UnityEngine.Object.DestroyImmediate(directorObject);
            }
        }

        [Test]
        public void LocationSceneController_AddsMarketBehavioursFromDefinition()
        {
            Type definitionType =
                FindType("CryingSnow.StackCraft.LocationDefinition");
            Type controllerType =
                FindType("CryingSnow.StackCraft.LocationSceneController");
            Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
            Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            Type vendorType =
                FindType("CryingSnow.StackCraft.MarketProductVendor");
            Type buyerType =
                FindType("CryingSnow.StackCraft.MarketCardBuyer");
            Assert.That(vendorType, Is.Not.Null);
            Assert.That(buyerType, Is.Not.Null);

            ScriptableObject definition =
                ScriptableObject.CreateInstance(definitionType);
            ScriptableObject vendorDefinition = CreateDefinition(
                "test-vendor", 6, false, 0, true);
            ScriptableObject buyerDefinition = CreateDefinition(
                "test-buyer", 6, false, 0, true);
            ScriptableObject productDefinition = CreateDefinition(
                "test-product", 4, true, 1, false);
            GameObject vendorObject = new("Vendor Card");
            GameObject buyerObject = new("Buyer Card");
            try
            {
                var serialized = new SerializedObject(definition);
                SerializedProperty buyer =
                    serialized.FindProperty("marketBuyerCardDefinition");
                SerializedProperty offers =
                    serialized.FindProperty("marketOffers");
                Assert.That(buyer, Is.Not.Null);
                Assert.That(offers, Is.Not.Null);
                buyer.objectReferenceValue = buyerDefinition;
                offers.arraySize = 1;
                SerializedProperty offer = offers.GetArrayElementAtIndex(0);
                offer.FindPropertyRelative("sourceCardDefinition")
                    .objectReferenceValue = vendorDefinition;
                offer.FindPropertyRelative("productDefinition")
                    .objectReferenceValue = productDefinition;
                offer.FindPropertyRelative("buyPrice").intValue = 3;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Component vendorCard = vendorObject.AddComponent(cardType);
                Component buyerCard = buyerObject.AddComponent(cardType);
                SetDefinition(vendorCard, vendorDefinition);
                SetDefinition(buyerCard, buyerDefinition);
                cardType.GetProperty("Stack")?.SetValue(
                    vendorCard,
                    Activator.CreateInstance(stackType, vendorCard, Vector3.zero));
                cardType.GetProperty("Stack")?.SetValue(
                    buyerCard,
                    Activator.CreateInstance(stackType, buyerCard, Vector3.right));

                controllerType.GetMethod(
                        "ConfigureLocationCardBehaviours",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[]
                        {
                            typeof(System.Collections.Generic.IEnumerable<>)
                                .MakeGenericType(cardType),
                            definitionType
                        },
                        null)
                    ?.Invoke(null, new object[]
                    {
                        CreateTypedArray(cardType, vendorCard, buyerCard),
                        definition
                    });

                Component vendor = vendorObject.GetComponent(vendorType);
                Component marketBuyer = buyerObject.GetComponent(buyerType);
                Assert.That(vendor, Is.Not.Null);
                Assert.That(marketBuyer, Is.Not.Null);
                Assert.That(vendorType.GetProperty("Product")?.GetValue(vendor),
                    Is.EqualTo(productDefinition));
                Assert.That(vendorType.GetProperty("BuyPrice")?.GetValue(vendor),
                    Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(vendorObject);
                UnityEngine.Object.DestroyImmediate(buyerObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(vendorDefinition);
                UnityEngine.Object.DestroyImmediate(buyerDefinition);
                UnityEngine.Object.DestroyImmediate(productDefinition);
            }
        }

        [Test]
        public void LocationScene_KeepsTradeServicesActiveWithoutLegacyZones()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/StackCraft/Scenes/Location.unity",
                OpenSceneMode.Additive);
            try
            {
                Component tradeManager = scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<Component>(true))
                    .FirstOrDefault(component =>
                        component != null &&
                        component.GetType().FullName ==
                        "CryingSnow.StackCraft.TradeManager");
                Assert.That(tradeManager, Is.Not.Null);
                Assert.That(tradeManager.gameObject.activeSelf, Is.True,
                    "地点场景需要保留货币、宝箱和任务使用的交易服务。");

                SerializedProperty spawnLegacyZones =
                    new SerializedObject(tradeManager)
                        .FindProperty("spawnLegacyZones");
                Assert.That(spawnLegacyZones, Is.Not.Null,
                    "交易服务需要能独立关闭旧的顶部卡包售卖区。");
                Assert.That(spawnLegacyZones.boolValue, Is.True,
                    "共享的地点场景不能把旧交易区全局关闭。");

                SerializedProperty excludedLocations =
                    new SerializedObject(tradeManager)
                        .FindProperty("legacyZoneExcludedLocationIds");
                Assert.That(excludedLocations, Is.Not.Null);
                Assert.That(
                    Enumerable.Range(0, excludedLocations.arraySize)
                        .Select(index =>
                            excludedLocations.GetArrayElementAtIndex(index)
                                .stringValue),
                    Does.Contain("riverbend-market"),
                    "只应在河湾市场隐藏旧交易区。");

                UnityEngine.Object configuredCurrency =
                    new SerializedObject(tradeManager)
                        .FindProperty("currencyCard")
                        .objectReferenceValue;
                UnityEngine.Object expectedCurrency =
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                        "Assets/StackCraft/Resources/Cards/Currencies/Card_Coral.asset");
                Assert.That(configuredCurrency, Is.EqualTo(expectedCurrency),
                    "地点市场、宝箱和出售任务必须统一使用金币。");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(type => type != null);
        }

        private static ScriptableObject CreateDefinition(
            string id,
            int categoryIndex,
            bool sellable,
            int sellPrice,
            bool isLocationStatic)
        {
            Type definitionType =
                FindType("CryingSnow.StackCraft.CardDefinition");
            ScriptableObject definition =
                ScriptableObject.CreateInstance(definitionType);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            serialized.FindProperty("category").enumValueIndex = categoryIndex;
            serialized.FindProperty("isSellable").boolValue = sellable;
            serialized.FindProperty("sellPrice").intValue = sellPrice;
            serialized.FindProperty("isLocationStatic").boolValue =
                isLocationStatic;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static void SetDefinition(
            Component card,
            UnityEngine.Object definition)
        {
            card.GetType()
                .GetProperty(
                    "Definition",
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(card, definition);
            UnityEngine.Object settings =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Settings/Default_Card_Settings.asset");
            Assert.That(settings, Is.Not.Null);
            card.GetType()
                .GetProperty(
                    "Settings",
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(card, settings);
        }

        private static Array CreateTypedArray(
            Type elementType,
            params Component[] values)
        {
            Array array = Array.CreateInstance(elementType, values.Length);
            for (int index = 0; index < values.Length; index++)
                array.SetValue(values[index], index);
            return array;
        }
    }
}
