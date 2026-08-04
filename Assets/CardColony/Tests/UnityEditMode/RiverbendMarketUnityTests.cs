using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
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
        public void LocationSceneController_AddsNpcTraderToEveryNeutralCharacter()
        {
            Type controllerType =
                FindType("CryingSnow.StackCraft.LocationSceneController");
            Type cardType = FindType("CryingSnow.StackCraft.CardInstance");
            Type stackType = FindType("CryingSnow.StackCraft.CardStack");
            Type npcTraderType =
                FindType("CryingSnow.StackCraft.NpcTrader");
            Assert.That(npcTraderType, Is.Not.Null,
                "通用 NPC 交易组件尚未实现。");

            ScriptableObject npcDefinition = CreateDefinition(
                "test-neutral-npc",
                2,
                false,
                0,
                true);
            GameObject npcObject = new("Neutral NPC");
            try
            {
                Component npcCard = npcObject.AddComponent(cardType);
                SetDefinition(npcCard, npcDefinition);
                cardType.GetProperty("Stack")?.SetValue(
                    npcCard,
                    Activator.CreateInstance(stackType, npcCard, Vector3.zero));

                controllerType.GetMethod(
                        "ConfigureLocationCardBehaviours",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[]
                        {
                            typeof(System.Collections.Generic.IEnumerable<>)
                                .MakeGenericType(cardType),
                            FindType("CryingSnow.StackCraft.LocationDefinition")
                        },
                        null)
                    ?.Invoke(null, new object[]
                    {
                        CreateTypedArray(cardType, npcCard),
                        null
                    });

                Assert.That(
                    npcObject.GetComponent(npcTraderType),
                    Is.Not.Null,
                    "所有中立人物 NPC 都应获得通用交易能力。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(npcObject);
                UnityEngine.Object.DestroyImmediate(npcDefinition);
            }
        }

        [Test]
        public void NpcTradeProfileResolver_UsesProfessionAndResidentFallbacks()
        {
            Type resolverType =
                FindType("CryingSnow.StackCraft.NpcTradeProfileResolver");
            Type categoryType =
                FindType("CryingSnow.StackCraft.CardCategory");
            Assert.That(resolverType, Is.Not.Null,
                "NPC 职业交易模板解析器尚未实现。");

            MethodInfo resolve = resolverType.GetMethod(
                "Resolve",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    FindType("CryingSnow.StackCraft.CardDefinition")
                },
                null);
            Assert.That(resolve, Is.Not.Null);

            ScriptableObject blacksmith = CreateDefinition(
                "riverbend-blacksmith",
                2,
                false,
                0,
                true);
            ScriptableObject apothecary = CreateDefinition(
                "riverbend-apothecary",
                2,
                false,
                0,
                true);
            ScriptableObject resident = CreateDefinition(
                "riverbend-resident",
                2,
                false,
                0,
                true);
            try
            {
                object blacksmithProfile =
                    resolve.Invoke(null, new object[] { blacksmith });
                object apothecaryProfile =
                    resolve.Invoke(null, new object[] { apothecary });
                object residentProfile =
                    resolve.Invoke(null, new object[] { resident });
                Assert.That(blacksmithProfile, Is.Not.Null);
                Assert.That(apothecaryProfile, Is.Not.Null);
                Assert.That(residentProfile, Is.Not.Null,
                    "未显式配置的 NPC 必须回退到普通居民交易模板。");

                MethodInfo canBuyCategory =
                    blacksmithProfile.GetType().GetMethod("CanBuyCategory");
                PropertyInfo roleLabel =
                    blacksmithProfile.GetType().GetProperty("RoleLabel");
                Assert.That(canBuyCategory, Is.Not.Null);
                Assert.That(roleLabel, Is.Not.Null);

                object equipment = Enum.Parse(categoryType, "Equipment");
                object consumable = Enum.Parse(categoryType, "Consumable");
                Assert.That(
                    canBuyCategory.Invoke(
                        blacksmithProfile,
                        new[] { equipment }),
                    Is.True);
                Assert.That(
                    canBuyCategory.Invoke(
                        blacksmithProfile,
                        new[] { consumable }),
                    Is.False);
                Assert.That(
                    canBuyCategory.Invoke(
                        apothecaryProfile,
                        new[] { consumable }),
                    Is.True);
                Assert.That(
                    canBuyCategory.Invoke(
                        apothecaryProfile,
                        new[] { equipment }),
                    Is.False);
                Assert.That(
                    roleLabel.GetValue(blacksmithProfile),
                    Is.Not.EqualTo(roleLabel.GetValue(apothecaryProfile)));
                Assert.That(
                    roleLabel.GetValue(residentProfile),
                    Is.EqualTo("居民"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(blacksmith);
                UnityEngine.Object.DestroyImmediate(apothecary);
                UnityEngine.Object.DestroyImmediate(resident);
            }
        }

        [Test]
        public void NpcTradeProfileResolver_PrefersExplicitNpcProfile()
        {
            Type profileType =
                FindType("CryingSnow.StackCraft.NpcTradeProfile");
            Type resolverType =
                FindType("CryingSnow.StackCraft.NpcTradeProfileResolver");
            Assert.That(profileType, Is.Not.Null);

            MethodInfo resolve = resolverType.GetMethod(
                "Resolve",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    FindType("CryingSnow.StackCraft.CardDefinition"),
                    profileType
                },
                null);
            Assert.That(resolve, Is.Not.Null,
                "解析顺序必须支持 NPC 显式配置优先。");
        }

        [Test]
        public void NpcTradeProfileResolver_AppliesRejectionsAndPriceModifiers()
        {
            Type profileType =
                FindType("CryingSnow.StackCraft.NpcTradeProfile");
            Type resolverType =
                FindType("CryingSnow.StackCraft.NpcTradeProfileResolver");
            ScriptableObject npc = CreateDefinition(
                "test-priced-npc",
                2,
                false,
                0,
                true);
            ScriptableObject product = CreateDefinition(
                "test-rejected-product",
                1,
                true,
                10,
                false);
            ScriptableObject profile =
                ScriptableObject.CreateInstance(profileType);
            try
            {
                var serialized = new SerializedObject(profile);
                serialized.FindProperty("npcDefinition")
                    .objectReferenceValue = npc;
                SerializedProperty categories =
                    serialized.FindProperty("buyCategoriesOverride");
                categories.arraySize = 1;
                categories.GetArrayElementAtIndex(0).enumValueIndex = 1;
                SerializedProperty rejected =
                    serialized.FindProperty("rejectedDefinitions");
                rejected.arraySize = 1;
                rejected.GetArrayElementAtIndex(0)
                    .objectReferenceValue = product;
                serialized.FindProperty("buyPriceModifier")
                    .floatValue = 0.75f;
                serialized.FindProperty("sellPriceModifier")
                    .floatValue = 1.5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                object resolved = resolverType.GetMethod(
                        "Resolve",
                        new[]
                        {
                            FindType("CryingSnow.StackCraft.CardDefinition"),
                            profileType
                        })
                    ?.Invoke(null, new object[] { npc, profile });
                Assert.That(resolved, Is.Not.Null);
                Assert.That(
                    resolved.GetType().GetMethod("CanBuyDefinition")
                        ?.Invoke(resolved, new object[] { product }),
                    Is.False,
                    "Explicitly rejected products must not be accepted.");
                Assert.That(
                    resolved.GetType().GetMethod("CalculatePlayerBuyPrice")
                        ?.Invoke(resolved, new object[] { 10 }),
                    Is.EqualTo(15));
                Assert.That(
                    resolved.GetType().GetMethod("CalculatePlayerSellPrice")
                        ?.Invoke(resolved, new object[] { 10 }),
                    Is.EqualTo(8));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(product);
                UnityEngine.Object.DestroyImmediate(npc);
            }
        }

        [Test]
        public void NpcSalePreview_DoesNotInterceptPlayerCharacterDialogue()
        {
            Type controllerType =
                FindType("CryingSnow.StackCraft.CardController");
            MethodInfo shouldPreview = controllerType.GetMethod(
                "ShouldAttemptNpcSalePreview",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(shouldPreview, Is.Not.Null);

            ScriptableObject player = CreateDefinition(
                "test-player",
                2,
                false,
                0,
                false);
            try
            {
                Assert.That(
                    shouldPreview.Invoke(null, new object[] { player }),
                    Is.False,
                    "玩家人物卡必须继续进入 NPC 对话流程，不能被出售预览截获。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void NpcTradeLedger_PersistsFundsAndAcquiredStockAcrossVisits()
        {
            Type sceneDataType =
                FindType("CryingSnow.StackCraft.SceneData");
            Type ledgerType =
                FindType("CryingSnow.StackCraft.NpcTradeLedger");
            Assert.That(ledgerType, Is.Not.Null,
                "NPC 个人库存与资金账本尚未实现。");

            object sceneData = Activator.CreateInstance(
                sceneDataType,
                new object[] { "Location/Test" });
            MethodInfo getOrRefresh = ledgerType.GetMethod(
                "GetOrRefresh",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo trySpendFunds = ledgerType.GetMethod(
                "TrySpendFunds",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo addAcquiredStock = ledgerType.GetMethod(
                "AddAcquiredStock",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo getAcquiredStock = ledgerType.GetMethod(
                "GetAcquiredStock",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(getOrRefresh, Is.Not.Null);
            Assert.That(trySpendFunds, Is.Not.Null);
            Assert.That(addAcquiredStock, Is.Not.Null);
            Assert.That(getAcquiredStock, Is.Not.Null);

            object state = getOrRefresh.Invoke(
                null,
                new[] { sceneData, "test-npc", 2, 12 });
            PropertyInfo funds = state.GetType().GetProperty("AvailableFunds");
            Assert.That(funds.GetValue(state), Is.EqualTo(12));
            Assert.That(
                trySpendFunds.Invoke(
                    null,
                    new[] { sceneData, "test-npc", 2, 12, 5 }),
                Is.True);
            Assert.That(
                funds.GetValue(getOrRefresh.Invoke(
                    null,
                    new[] { sceneData, "test-npc", 2, 12 })),
                Is.EqualTo(7),
                "同一天重新进入地点不能刷新 NPC 资金。");

            addAcquiredStock.Invoke(
                null,
                new[] { sceneData, "test-npc", 2, 12, "test-apple", 3 });
            Assert.That(
                getAcquiredStock.Invoke(
                    null,
                    new[] { sceneData, "test-npc", 2, 12, "test-apple" }),
                Is.EqualTo(3));

            object nextDay = getOrRefresh.Invoke(
                null,
                new[] { sceneData, "test-npc", 3, 12 });
            Assert.That(funds.GetValue(nextDay), Is.EqualTo(12),
                "跨日后 NPC 资金应按配置恢复。");
            Assert.That(
                getAcquiredStock.Invoke(
                    null,
                    new[] { sceneData, "test-npc", 3, 12, "test-apple" }),
                Is.EqualTo(3),
                "玩家卖给 NPC 的个人物品应跨日保留。");
        }

        [Test]
        public void MarketStockLedger_ScopesLegacyOfferStockPerNpc()
        {
            Type sceneDataType =
                FindType("CryingSnow.StackCraft.SceneData");
            Type stockDataType =
                FindType("CryingSnow.StackCraft.MarketStockData");
            Type ledgerType =
                FindType("CryingSnow.StackCraft.MarketStockLedger");
            object sceneData = Activator.CreateInstance(
                sceneDataType,
                new object[] { "Location/Test" });
            System.Collections.IList marketStock =
                (System.Collections.IList)sceneDataType
                    .GetField("MarketStock").GetValue(sceneData);
            marketStock.Add(Activator.CreateInstance(
                stockDataType,
                new object[] { "shared-offer", 2, 3 }));

            MethodInfo getNpcStock = ledgerType.GetMethod(
                "GetOrRefreshNpc",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo consumeNpcStock = ledgerType.GetMethod(
                "TryConsumeNpc",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(getNpcStock, Is.Not.Null);
            Assert.That(consumeNpcStock, Is.Not.Null);

            object npcA = getNpcStock.Invoke(
                null,
                new[]
                {
                    sceneData, "npc-a", "shared-offer", 2, 1, 5
                });
            Assert.That(
                stockDataType.GetProperty("Remaining").GetValue(npcA),
                Is.EqualTo(3),
                "Legacy stall stock should seed the first personal stock.");
            Assert.That(
                consumeNpcStock.Invoke(
                    null,
                    new[]
                    {
                        sceneData, "npc-a", "shared-offer", 2, 1, 5
                    }),
                Is.True);

            object npcB = getNpcStock.Invoke(
                null,
                new[]
                {
                    sceneData, "npc-b", "shared-offer", 2, 1, 5
                });
            Assert.That(
                stockDataType.GetProperty("Remaining").GetValue(npcA),
                Is.EqualTo(2));
            Assert.That(
                stockDataType.GetProperty("Remaining").GetValue(npcB),
                Is.EqualTo(3),
                "NPCs reusing an offer id must not share consumed stock.");
        }

        [Test]
        public void WorldMapLocationView_ProvidesGenericNpcActionEntry()
        {
            Type viewType =
                FindType("CryingSnow.StackCraft.WorldMapLocationView");
            Assert.That(
                viewType.GetMethod(
                    "PopulateNpcActions",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "The action tab must create contextual NPC actions.");
            Assert.That(
                viewType.GetMethod(
                    "StartNpcDialogue",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "The talk entry must call the base dialogue flow.");
        }

        [Test]
        public void RiverbendMarket_UsesGrocerInsteadOfStallAndBuyerCards()
        {
            UnityEngine.Object market =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MarketPath);
            UnityEngine.Object grocer =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Cards/Locations/Riverbend/Card_Riverbend_Grocer.asset");
            Assert.That(market, Is.Not.Null);
            Assert.That(grocer, Is.Not.Null);

            var serialized = new SerializedObject(market);
            Assert.That(
                serialized.FindProperty("marketBuyerCardDefinition")
                    .objectReferenceValue,
                Is.Null);

            SerializedProperty spawns =
                serialized.FindProperty("initialCardSpawns");
            for (int index = 0; index < spawns.arraySize; index++)
            {
                UnityEngine.Object definition = spawns
                    .GetArrayElementAtIndex(index)
                    .FindPropertyRelative("definition")
                    .objectReferenceValue;
                string id = definition == null
                    ? string.Empty
                    : new SerializedObject(definition)
                        .FindProperty("id").stringValue;
                Assert.That(id, Does.Not.Contain("-stall"));
                Assert.That(id, Is.Not.EqualTo("riverbend-market-buyer"));
            }

            SerializedProperty offers = serialized.FindProperty("marketOffers");
            Assert.That(offers.arraySize, Is.EqualTo(7));
            for (int index = 0; index < offers.arraySize; index++)
            {
                Assert.That(
                    offers.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("sourceCardDefinition")
                        .objectReferenceValue,
                    Is.EqualTo(grocer));
            }
        }

        [Test]
        public void RiverbendMarketMigration_RemovesOnlyLegacyServiceCards()
        {
            Type sceneDataType = FindType("CryingSnow.StackCraft.SceneData");
            Type stackDataType = FindType("CryingSnow.StackCraft.StackData");
            Type cardDataType = FindType("CryingSnow.StackCraft.CardData");
            object sceneData = Activator.CreateInstance(
                sceneDataType,
                new object[] { "Location/riverbend-market" });
            System.Collections.IList savedStacks =
                (System.Collections.IList)sceneDataType
                    .GetField("SavedStacks").GetValue(sceneData);

            object mixedStack = Activator.CreateInstance(stackDataType);
            stackDataType.GetField("Position").SetValue(
                mixedStack,
                new[] { 0f, 0f, 0f });
            System.Collections.IList mixedCards =
                (System.Collections.IList)stackDataType
                    .GetField("Cards").GetValue(mixedStack);
            mixedCards.Add(CreateCardData(
                cardDataType,
                "riverbend-market-apple-stall"));
            mixedCards.Add(CreateCardData(
                cardDataType,
                "test-player-owned-apple"));
            savedStacks.Add(mixedStack);

            object retiredStack = Activator.CreateInstance(stackDataType);
            stackDataType.GetField("Position").SetValue(
                retiredStack,
                new[] { 1f, 0f, 0f });
            System.Collections.IList retiredCards =
                (System.Collections.IList)stackDataType
                    .GetField("Cards").GetValue(retiredStack);
            retiredCards.Add(CreateCardData(
                cardDataType,
                "riverbend-market-buyer"));
            savedStacks.Add(retiredStack);

            Type managerType = FindType("CryingSnow.StackCraft.CardManager");
            MethodInfo migrate = managerType.GetMethod(
                "MigrateRetiredRiverbendInitialCards",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(migrate, Is.Not.Null);
            migrate.Invoke(null, new object[]
            {
                sceneData,
                "riverbend-market"
            });

            Assert.That(savedStacks.Count, Is.EqualTo(1));
            System.Collections.IList remainingCards =
                (System.Collections.IList)stackDataType
                    .GetField("Cards").GetValue(savedStacks[0]);
            Assert.That(
                remainingCards.Cast<object>().Select(card =>
                    cardDataType.GetField("Id").GetValue(card)),
                Is.EqualTo(new[] { "test-player-owned-apple" }));
            Assert.That(
                sceneDataType.GetField("ContentMigrationVersion")
                    .GetValue(sceneData),
                Is.EqualTo(1));
        }

        [Test]
        public void UiRoot_ContainsNpcTradeTabsAndScrollableList()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(FindChild(prefab.transform, "NpcTradePanel"), Is.Not.Null);
            Assert.That(FindChild(prefab.transform, "NpcBuyTabButton"), Is.Not.Null);
            Assert.That(FindChild(prefab.transform, "NpcSellTabButton"), Is.Not.Null);
            Assert.That(FindChild(prefab.transform, "NpcActionTabButton"), Is.Not.Null,
                "人物侧栏需要统一的行动页签，交谈不再作为拖拽后的默认动作。");
            Assert.That(FindChild(prefab.transform, "NpcTradeScrollView"), Is.Not.Null);
            Assert.That(FindChild(prefab.transform, "NpcTradeRowTemplate"), Is.Not.Null);

            Type viewType =
                FindType("CryingSnow.StackCraft.WorldMapLocationView");
            Type traderType = FindType("CryingSnow.StackCraft.NpcTrader");
            Assert.That(viewType.GetProperty("SelectedNpcTrader"), Is.Not.Null);
            Assert.That(
                viewType.GetMethod("ShowNpcTrader", new[] { traderType }),
                Is.Not.Null);
            Assert.That(
                viewType.GetMethod("ShowNpcActions"),
                Is.Not.Null);
            Assert.That(
                viewType.GetMethod("StartSelectedNpcInteraction"),
                Is.Not.Null);
            Assert.That(
                viewType.GetMethod("EndSelectedNpcInteraction"),
                Is.Not.Null);
        }

        [Test]
        public void UiRoot_ContainsDirectPublicMarketOverview()
        {
            Type screenType = FindType(
                "CryingSnow.StackCraft.PublicMarketTradeScreen");
            Type rowType = FindType(
                "CryingSnow.StackCraft.MarketCommodityListItem");
            Assert.That(screenType, Is.Not.Null,
                "地点直达的公共市场总览控制器尚未实现。");
            Assert.That(rowType, Is.Not.Null,
                "公共市场需要独立的列式商品行，不能复用窄版 NPC 交易行。");
            Assert.That(
                screenType.GetProperty("NpcTrader"),
                Is.Null,
                "公共市场总览不能依赖 NpcTrader。");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(
                prefab.GetComponentInChildren(screenType, true),
                Is.Not.Null);
            Assert.That(
                prefab.GetComponentInChildren(rowType, true),
                Is.Not.Null);
            Assert.That(
                FindChild(prefab.transform, "LocalMarketButton"),
                Is.Not.Null,
                "地点界面需要固定市场按钮。");
            Assert.That(
                FindChild(prefab.transform, "PublicMarketModal"),
                Is.Not.Null);
            Assert.That(
                FindChild(prefab.transform, "PublicMarketMarketListContent"),
                Is.Not.Null);
            Assert.That(
                FindChild(prefab.transform, "PublicMarketMarketRowTemplate"),
                Is.Not.Null);
            Assert.That(
                FindChild(prefab.transform, "PublicMarketConfirmButton"),
                Is.Not.Null);
        }

        [Test]
        public void UiRoot_PublicMarketUsesDualInventoryTradeLayout()
        {
            Type screenType = FindType(
                "CryingSnow.StackCraft.PublicMarketTradeScreen");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Assert.That(prefab, Is.Not.Null);

            Transform backpackList = FindChild(
                prefab.transform,
                "PublicMarketBackpackListContent");
            Transform transactionPanel = FindChild(
                prefab.transform,
                "PublicMarketTransactionPanel");
            Transform marketList = FindChild(
                prefab.transform,
                "PublicMarketMarketListContent");
            Assert.That(backpackList, Is.Not.Null,
                "交易界面左侧必须持续显示玩家背包，而不是只在市场表里放一个持有数字");
            Assert.That(transactionPanel, Is.Not.Null,
                "双库存之间需要固定交易单，统一显示数量、总价和交易后状态");
            Assert.That(marketList, Is.Not.Null,
                "交易界面右侧必须显示当地市场库存");
            Assert.That(
                FindChild(prefab.transform, "PublicMarketBuyModeButton"),
                Is.Null,
                "点击市场商品应直接进入购买，不再要求先切换购买页签");
            Assert.That(
                FindChild(prefab.transform, "PublicMarketSellModeButton"),
                Is.Null,
                "点击背包商品应直接进入出售，不再要求先切换出售页签");

            RectTransform backpackPanel = (RectTransform)FindChild(
                prefab.transform,
                "PublicMarketBackpackPanel");
            RectTransform marketPanel = (RectTransform)FindChild(
                prefab.transform,
                "PublicMarketMarketPanel");
            Assert.That(backpackPanel.anchorMax.x, Is.LessThanOrEqualTo(0.35f));
            Assert.That(transactionPanel.GetComponent<RectTransform>()
                .anchorMin.x, Is.GreaterThanOrEqualTo(0.35f));
            Assert.That(marketPanel.anchorMin.x, Is.GreaterThanOrEqualTo(0.65f));

            Component screen = prefab.GetComponentInChildren(
                screenType,
                true);
            var serializedScreen = new SerializedObject(screen);
            Assert.That(
                serializedScreen.FindProperty("backpackListRoot")
                    ?.objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serializedScreen.FindProperty("backpackRowTemplate")
                    ?.objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serializedScreen.FindProperty("marketListRoot")
                    ?.objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serializedScreen.FindProperty("marketRowTemplate")
                    ?.objectReferenceValue,
                Is.Not.Null);
        }

        [Test]
        public void UiRoot_PublicMarketUsesLightSkinWithoutReplacingBindings()
        {
            const string panelSpritePath =
                "Assets/UltimateCleanGUIPack/Common/Sprites/Shapes/" +
                "Semi Rounded/Semi Rounded - 300ppu.png";
            const string borderSpritePath =
                "Assets/UltimateCleanGUIPack/Common/Sprites/Shapes/" +
                "Semi Rounded/" +
                "Semi Rounded - Outline - 6px - 300ppu.png";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Assert.That(prefab, Is.Not.Null);

            Transform modal = FindChild(
                prefab.transform,
                "PublicMarketModal");
            Assert.That(modal, Is.Not.Null);
            AssertSlicedSprite(
                modal.GetComponent<Image>(),
                panelSpritePath);

            Transform frame = FindChild(
                modal,
                "PublicMarketFantasyFrame");
            Assert.That(frame, Is.Not.Null,
                "公共市场需要独立的奇幻边框，不能只靠纯色背景。");
            AssertSlicedSprite(
                frame.GetComponent<Image>(),
                borderSpritePath);
            Assert.That(
                frame.GetComponent<Image>().raycastTarget,
                Is.False);

            foreach (string panelName in new[]
                     {
                         "PublicMarketBackpackPanel",
                         "PublicMarketTransactionPanel",
                         "PublicMarketMarketPanel"
                     })
            {
                Transform panel = FindChild(modal, panelName);
                Assert.That(panel, Is.Not.Null);
                AssertSlicedSprite(
                    panel.GetComponent<Image>(),
                    panelSpritePath);
            }

            AssertSlicedSprite(
                FindChild(
                        modal,
                        "PublicMarketBackpackRowTemplate")
                    .GetComponent<Image>(),
                panelSpritePath);
            AssertSlicedSprite(
                FindChild(
                        modal,
                        "PublicMarketMarketRowTemplate")
                    .GetComponent<Image>(),
                panelSpritePath);

            foreach (string buttonName in new[]
                     {
                         "PublicMarketCloseButton",
                         "PublicMarketDecreaseButton",
                         "PublicMarketIncreaseButton",
                         "PublicMarketMaximumButton",
                         "PublicMarketConfirmButton"
                     })
            {
                Transform button = FindChild(modal, buttonName);
                Assert.That(button, Is.Not.Null);
                Image buttonImage = button.GetComponent<Image>();
                AssertSlicedSprite(buttonImage, panelSpritePath);
                Assert.That(ColorLuminance(buttonImage.color),
                    Is.InRange(0.62f, 0.92f),
                    "彩色凸面按钮已经包含完整配色，不能再乘主题色。");
                ColorBlock colors =
                    button.GetComponent<Button>().colors;
                Assert.That(colors.normalColor, Is.EqualTo(Color.white),
                    "按钮底图已经带主题色，Normal Color 必须保持白色，" +
                    "否则颜色会被重复相乘而显得过暗。");
                Assert.That(
                    button.GetComponent<Button>().navigation.mode,
                    Is.EqualTo(Navigation.Mode.Automatic),
                    "换肤只能修改视觉，不能移除键盘和手柄按钮导航。");
            }

            TMP_Text title = FindChild(
                modal,
                "PublicMarketTitle").GetComponent<TMP_Text>();
            Assert.That(
                AssetDatabase.GetAssetPath(title.font),
                Does.EndWith("SIMYOU SDF.asset"),
                "公共市场中文必须继续使用项目中文字体。");

            Type screenType = FindType(
                "CryingSnow.StackCraft.PublicMarketTradeScreen");
            Component screen = prefab.GetComponentInChildren(
                screenType,
                true);
            Assert.That(screen, Is.Not.Null);
            var serializedScreen = new SerializedObject(screen);
            Assert.That(
                serializedScreen.FindProperty("modalRoot")
                    ?.objectReferenceValue,
                Is.EqualTo(modal.gameObject),
                "换肤不能替换公共市场控制器原有的模态框绑定。");
        }

        [Test]
        public void UiRoot_PublicMarketLightSkinUsesContrastAndDepth()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Transform modal = FindChild(
                prefab.transform,
                "PublicMarketModal");

            Image backpack = FindChild(
                modal,
                "PublicMarketBackpackPanel").GetComponent<Image>();
            Image transaction = FindChild(
                modal,
                "PublicMarketTransactionPanel").GetComponent<Image>();
            Image market = FindChild(
                modal,
                "PublicMarketMarketPanel").GetComponent<Image>();

            Assert.That(ColorLuminance(backpack.color),
                Is.InRange(0.62f, 0.96f),
                "背包区需要明显的蓝色识别，而不是接近灰色。");
            Assert.That(ColorLuminance(transaction.color),
                Is.InRange(0.62f, 0.96f),
                "交易区需要明显的琥珀暖色识别。");
            Assert.That(ColorLuminance(market.color),
                Is.InRange(0.62f, 0.96f),
                "市场区需要明显的绿色识别。");

            TMP_Text backpackTitle = FindChild(
                modal,
                "PublicMarketBackpackPanelTitle").GetComponent<TMP_Text>();
            TMP_Text backpackSubtitle = FindChild(
                modal,
                "PublicMarketBackpackPanelSubtitle").GetComponent<TMP_Text>();
            Assert.That(ColorLuminance(backpackTitle.color),
                Is.LessThan(0.52f),
                "左栏标题需要使用青蓝色建立栏目识别。");
            Assert.That(ColorLuminance(backpackSubtitle.color),
                Is.LessThan(0.52f),
                "左栏说明文字需要使用暖白色，与青蓝标题形成配色层次。");

            Assert.That(
                FindChild(modal, "PublicMarketFantasyHeaderHighlight"),
                Is.Null,
                "整块白色标题高光会在界面上留下灰白残片。");
            foreach (string panelName in new[]
                     {
                         "PublicMarketBackpackPanel",
                         "PublicMarketTransactionPanel",
                         "PublicMarketMarketPanel"
                     })
            {
                Transform panel = FindChild(modal, panelName);
                AssertDepthShadow(panel);
                Assert.That(
                    FindChild(panel, $"{panelName}TopHighlight"),
                    Is.Null,
                    "整块白色面板高光会冲淡背景和文字。");
                Assert.That(
                    FindChild(panel, $"{panelName}BottomShade"),
                    Is.Null,
                    "旧的整块覆盖层应一并清理。");
            }

            foreach (string rowName in new[]
                     {
                         "PublicMarketBackpackRowTemplate",
                         "PublicMarketMarketRowTemplate"
                     })
            {
                AssertDepthShadow(FindChild(modal, rowName));
            }

            AssertLightButton(modal, "PublicMarketCloseButton");
            AssertLightButton(modal, "PublicMarketDecreaseButton");
            AssertLightButton(modal, "PublicMarketIncreaseButton");
            AssertLightButton(modal, "PublicMarketMaximumButton");
            AssertLightButton(modal, "PublicMarketConfirmButton");
            Color close = FindChild(modal, "PublicMarketCloseButton")
                .GetComponent<Image>().color;
            Color maximum = FindChild(modal, "PublicMarketMaximumButton")
                .GetComponent<Image>().color;
            Color confirm = FindChild(modal, "PublicMarketConfirmButton")
                .GetComponent<Image>().color;
            Assert.That(close.r, Is.GreaterThan(close.g + 0.05f));
            Assert.That(maximum.r, Is.GreaterThan(maximum.b + 0.20f));
            Assert.That(confirm.g, Is.GreaterThan(confirm.r + 0.05f));
        }

        [Test]
        public void WorldMapLocationUiInstaller_MarketReplacementAppliesLightSkin()
        {
            const string panelSpritePath =
                "Assets/UltimateCleanGUIPack/Common/Sprites/Shapes/" +
                "Semi Rounded/Semi Rounded - 300ppu.png";

            Type installerType = FindType(
                "CryingSnow.StackCraft.EditorTools." +
                "WorldMapLocationUiPrefabInstaller");
            MethodInfo replace = installerType?.GetMethod(
                "ReplacePublicMarketWithDualInventory",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(replace, Is.Not.Null);

            GameObject root = PrefabUtility.LoadPrefabContents(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            try
            {
                Transform canvas = FindChild(root.transform, "UICanvas");
                Transform marketButton = FindChild(
                    root.transform,
                    "LocalMarketButton");
                Transform questsToggle = FindChild(
                    root.transform,
                    "QuestsToggle");
                TMP_FontAsset font = questsToggle
                    .GetComponentInChildren<TMP_Text>(true)
                    .font;

                replace.Invoke(
                    null,
                    new object[]
                    {
                        root,
                        canvas,
                        font,
                        marketButton.GetComponent<Button>()
                    });

                Transform modal = FindChild(
                    root.transform,
                    "PublicMarketModal");
                Assert.That(modal, Is.Not.Null);
                AssertSlicedSprite(
                    modal.GetComponent<Image>(),
                    panelSpritePath);
                Assert.That(
                    FindChild(modal, "PublicMarketFantasyFrame"),
                    Is.Not.Null,
                    "旧市场升级成三栏布局时必须在同一次执行中完成换肤。");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void WorldMapLocationUiInstaller_NullRootLookupIsSafe()
        {
            Type installerType = FindType(
                "CryingSnow.StackCraft.EditorTools." +
                "WorldMapLocationUiPrefabInstaller");
            MethodInfo findDescendant = installerType?.GetMethod(
                "FindDescendant",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(findDescendant, Is.Not.Null);

            object result = null;
            Assert.DoesNotThrow(() =>
                result = findDescendant.Invoke(
                    null,
                    new object[] { null, "PublicMarketModal" }));
            Assert.That(result, Is.Null);
        }

        private static void AssertSlicedSprite(
            Image image,
            string expectedAssetPath)
        {
            Assert.That(image, Is.Not.Null);
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(image.sprite),
                Is.EqualTo(expectedAssetPath));
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
        }

        private static void AssertDepthShadow(Transform element)
        {
            Assert.That(element, Is.Not.Null);
            Shadow shadow = element.GetComponent<Shadow>();
            Assert.That(shadow, Is.Not.Null,
                $"{element.name} 缺少投影层次。");
            Assert.That(shadow.effectDistance.y, Is.LessThan(-1f));
            Assert.That(shadow.effectColor.a, Is.InRange(0.12f, 0.30f));
        }

        private static void AssertLightButton(
            Transform modal,
            string buttonName)
        {
            string expectedPath =
                "Assets/UltimateCleanGUIPack/Common/Sprites/Shapes/" +
                "Semi Rounded/Semi Rounded - 300ppu.png";
            Transform button = FindChild(modal, buttonName);
            AssertSlicedSprite(
                button.GetComponent<Image>(),
                expectedPath);
            Assert.That(
                ColorLuminance(button.GetComponent<Image>().color),
                Is.InRange(0.62f, 0.92f));
            AssertDepthShadow(button);
        }

        private static float ColorLuminance(Color color)
        {
            return 0.2126f * color.r +
                   0.7152f * color.g +
                   0.0722f * color.b;
        }

        [Test]
        public void PublicMarketDualInventoryRowsShowDirectionSpecificPrices()
        {
            Type rowType = FindType(
                "CryingSnow.StackCraft.MarketCommodityListItem");
            Type quoteType = FindType(
                "CryingSnow.StackCraft.MarketQuote");
            Type directionType = FindType(
                "CryingSnow.StackCraft.MarketTradeDirection");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Transform backpackTemplate = FindChild(
                prefab.transform,
                "PublicMarketBackpackRowTemplate");
            Transform marketTemplate = FindChild(
                prefab.transform,
                "PublicMarketMarketRowTemplate");
            Assert.That(backpackTemplate, Is.Not.Null);
            Assert.That(marketTemplate, Is.Not.Null);

            object quote = Activator.CreateInstance(
                quoteType,
                new object[]
                {
                    "riverbend-market",
                    "salt",
                    11,
                    8,
                    22,
                    10,
                    Enum.Parse(
                        FindType("CryingSnow.StackCraft.MarketTrend"),
                        "Normal"),
                    0L,
                    1
                });
            object playerSells = Enum.Parse(directionType, "PlayerSells");
            object playerBuys = Enum.Parse(directionType, "PlayerBuys");
            MethodInfo bind = rowType.GetMethod("Bind");

            bind.Invoke(
                backpackTemplate.GetComponent(rowType),
                new[] { null, quote, 3, playerSells, true, null });
            bind.Invoke(
                marketTemplate.GetComponent(rowType),
                new[] { null, quote, 3, playerBuys, true, null });

            Color sellColor =
                backpackTemplate.GetComponent<Image>().color;
            Color buyColor =
                marketTemplate.GetComponent<Image>().color;
            Assert.That(ColorLuminance(sellColor), Is.InRange(0.62f, 0.92f),
                "完整彩色凸面按钮不能再叠加背景染色。");
            Assert.That(ColorLuminance(buyColor), Is.InRange(0.62f, 0.92f),
                "完整彩色凸面按钮不能再叠加背景染色。");
            Assert.That(
                AssetDatabase.GetAssetPath(
                    backpackTemplate.GetComponent<Image>().sprite),
                Does.EndWith(
                    "Semi Rounded/Semi Rounded - 300ppu.png"),
                "背包出售条目需要使用棕色凸面按钮。");
            Assert.That(
                AssetDatabase.GetAssetPath(
                    marketTemplate.GetComponent<Image>().sprite),
                Does.EndWith(
                    "Semi Rounded/Semi Rounded - 300ppu.png"),
                "市场购买条目需要使用蓝色凸面按钮。");

            TMP_Text marketName = FindChild(
                marketTemplate,
                "Name").GetComponent<TMP_Text>();
            TMP_Text marketPrice = FindChild(
                marketTemplate,
                "Price").GetComponent<TMP_Text>();
            TMP_Text marketQuantity = FindChild(
                marketTemplate,
                "Quantity").GetComponent<TMP_Text>();
            TMP_Text marketDetails = FindChild(
                marketTemplate,
                "Details").GetComponent<TMP_Text>();
            TMP_Text marketTrend = FindChild(
                marketTemplate,
                "Trend").GetComponent<TMP_Text>();
            Assert.That(ColorLuminance(marketName.color),
                Is.LessThan(0.35f),
                "商品名使用暖象牙白，不能与蓝色按钮同色。");
            Assert.That(marketPrice.color.r,
                Is.GreaterThan(marketPrice.color.b + 0.20f),
                "价格需要使用金色。");
            Assert.That(marketQuantity.color.b,
                Is.GreaterThan(marketQuantity.color.r + 0.10f),
                "库存需要使用浅蓝色。");
            Assert.That(ColorLuminance(marketDetails.color),
                Is.InRange(0.25f, 0.50f),
                "辅助说明需要使用清晰的中性浅灰。");
            Assert.That(marketTrend.color.g,
                Is.GreaterThan(marketTrend.color.r + 0.10f),
                "行情状态需要使用灰绿色。");

            Assert.That(
                FindChild(backpackTemplate, "Price")
                    .GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("8"),
                "背包商品必须直接显示当地市场愿意支付的单价");
            Assert.That(
                FindChild(backpackTemplate, "Quantity")
                    .GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("3"));
            Assert.That(
                FindChild(marketTemplate, "Price")
                    .GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("11"),
                "市场商品必须直接显示玩家需要支付的单价");
            Assert.That(
                FindChild(marketTemplate, "Quantity")
                    .GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("22"));
        }

        [Test]
        public void RiverbendAndWhiteStoneLocations_ReferencePublicMarketsDirectly()
        {
            UnityEngine.Object riverbend =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    MarketPath);
            UnityEngine.Object whiteStone =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Locations/Location_WhiteStoneCity.asset");
            UnityEngine.Object riverbendMarket =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Trading/Markets/Market_riverbend-market.asset");
            UnityEngine.Object whiteStoneMarket =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StackCraft/Resources/Trading/Markets/Market_whitestone-market.asset");
            Assert.That(riverbend, Is.Not.Null);
            Assert.That(whiteStone, Is.Not.Null);

            Assert.That(
                new SerializedObject(riverbend)
                    .FindProperty("publicMarketProfile")
                    ?.objectReferenceValue,
                Is.EqualTo(riverbendMarket));
            Assert.That(
                new SerializedObject(whiteStone)
                    .FindProperty("publicMarketProfile")
                    ?.objectReferenceValue,
                Is.EqualTo(whiteStoneMarket));
            Assert.That(
                new SerializedObject(whiteStoneMarket)
                    .FindProperty("locationId")
                    ?.stringValue,
                Is.EqualTo("white-stone-city"));
        }

        [Test]
        public void WorldMapLocationView_ShowNpcTraderWithoutPendingSale_OpensSidebar()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/StackCraft/Scenes/Location.unity",
                OpenSceneMode.Additive);
            GameObject npcObject = new("NPC Trade Sidebar Test");
            ScriptableObject npcDefinition = CreateDefinition(
                "test-sidebar-npc",
                2,
                false,
                0,
                false);
            PropertyInfo cardManagerInstance = null;
            object previousCardManager = null;
            GameObject interactionHost = null;
            PropertyInfo interactionInstance = null;
            object previousInteraction = null;
            object interaction = null;
            try
            {
                Type viewType =
                    FindType("CryingSnow.StackCraft.WorldMapLocationView");
                Type cardType =
                    FindType("CryingSnow.StackCraft.CardInstance");
                Type stackType =
                    FindType("CryingSnow.StackCraft.CardStack");
                Type traderType =
                    FindType("CryingSnow.StackCraft.NpcTrader");
                Type cardManagerType =
                    FindType("CryingSnow.StackCraft.CardManager");
                Component view = scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<Component>(true))
                    .FirstOrDefault(component =>
                        component != null &&
                        component.GetType() == viewType);
                Assert.That(view, Is.Not.Null);

                Component cardManager = scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<Component>(true))
                    .FirstOrDefault(component =>
                        component != null &&
                        component.GetType() == cardManagerType);
                Assert.That(cardManager, Is.Not.Null);
                cardManagerInstance = cardManagerType.GetProperty(
                    "Instance",
                    BindingFlags.Public | BindingFlags.Static);
                previousCardManager = cardManagerInstance?.GetValue(null);
                cardManagerInstance?.SetValue(null, cardManager);

                Component card = npcObject.AddComponent(cardType);
                SetDefinition(card, npcDefinition);
                cardType.GetProperty("Stack")?.SetValue(
                    card,
                    Activator.CreateInstance(stackType, card, Vector3.zero));
                Component trader = npcObject.AddComponent(traderType);
                traderType.GetMethod(
                        "Configure",
                        new[] { cardType })
                    ?.Invoke(trader, new[] { card });

                Assert.DoesNotThrow(() =>
                    viewType.GetMethod(
                            "ShowNpcTrader",
                            new[] { traderType })
                        ?.Invoke(view, new[] { trader }),
                    "没有待售物品时，点击 NPC 也必须能打开交易侧栏。");

                Assert.That(
                    viewType.GetProperty("SelectedNpcTrader")
                        ?.GetValue(view),
                    Is.EqualTo(trader));
                CanvasGroup canvasGroup =
                    ((Component)view).GetComponent<CanvasGroup>();
                GameObject tradePanel = viewType.GetField(
                        "npcTradePanel",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(view) as GameObject;
                Assert.That(canvasGroup.alpha, Is.EqualTo(1f));
                Assert.That(canvasGroup.interactable, Is.True);
                Assert.That(tradePanel, Is.Not.Null);
                Assert.That(tradePanel.activeSelf, Is.True);

                Button buyTab = viewType.GetField(
                        "npcBuyTabButton",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(view) as Button;
                Button sellTab = viewType.GetField(
                        "npcSellTabButton",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(view) as Button;
                Button actionTab = viewType.GetField(
                        "npcActionTabButton",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(view) as Button;
                TMP_Text discovery = viewType.GetField(
                        "discoveryLabel",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(view) as TMP_Text;
                TMP_Text travelTime = viewType.GetField(
                        "travelTimeLabel",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(view) as TMP_Text;
                TMP_Text resources = viewType.GetField(
                        "resourcesLabel",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(view) as TMP_Text;
                TMP_Text description = viewType.GetField(
                        "descriptionLabel",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(view) as TMP_Text;

                Assert.That(buyTab.gameObject.activeSelf, Is.False,
                    "Buy and sell navigation should only appear after choosing Trade.");
                Assert.That(sellTab.gameObject.activeSelf, Is.False);
                Assert.That(actionTab.gameObject.activeSelf, Is.False);
                Assert.That(discovery.gameObject.activeSelf, Is.False,
                    "The compact NPC header must not retain the location detail rows.");
                Assert.That(travelTime.gameObject.activeSelf, Is.False);
                Assert.That(resources.gameObject.activeSelf, Is.False);
                Assert.That(
                    description.rectTransform.anchorMin.y,
                    Is.GreaterThanOrEqualTo(0.62f),
                    "NPC description should remain in the compact header above the action list.");

                Type interactionType =
                    FindType("CryingSnow.StackCraft.NpcInteractionManager");
                interactionInstance = interactionType.GetProperty(
                    "Instance",
                    BindingFlags.Public | BindingFlags.Static);
                previousInteraction = interactionInstance?.GetValue(null);
                interactionInstance?.SetValue(null, null);
                interactionHost = new GameObject(
                    "NPC Trade Navigation State Test");
                interaction = interactionHost.AddComponent(interactionType);
                interactionInstance?.SetValue(null, interaction);
                interactionType.GetField(
                        "player",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(interaction, card);
                interactionType.GetField(
                        "npc",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(interaction, card);
                interactionType.GetField(
                        "initiator",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(interaction, card);
                interactionType.GetField(
                        "target",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(interaction, card);
                FieldInfo stateField = interactionType.GetField(
                    "<State>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                object choosingAction = Enum.Parse(
                    stateField.FieldType,
                    "ChoosingAction");
                object tradeState = Enum.Parse(
                    stateField.FieldType,
                    "Trade");
                stateField.SetValue(interaction, choosingAction);
                traderType.GetProperty("PendingWorldSale")
                    ?.SetValue(
                        trader,
                        cardType.GetProperty("Stack")?.GetValue(card));

                viewType.GetMethod(
                        "ShowNpcTrader",
                        new[] { traderType })
                    ?.Invoke(view, new[] { trader });
                Assert.That(buyTab.gameObject.activeSelf, Is.False,
                    "A pending sale must not bypass the action choice state.");
                Assert.That(sellTab.gameObject.activeSelf, Is.False);
                Assert.That(actionTab.gameObject.activeSelf, Is.False);

                stateField.SetValue(interaction, tradeState);
                viewType.GetMethod(
                        "ShowNpcTrader",
                        new[] { traderType })
                    ?.Invoke(view, new[] { trader });
                Assert.That(buyTab.gameObject.activeSelf, Is.True,
                    "Trade navigation should appear after the interaction enters Trade.");
                Assert.That(sellTab.gameObject.activeSelf, Is.True);
                Assert.That(actionTab.gameObject.activeSelf, Is.True);

                viewType.GetMethod("ShowNpcSellList")
                    ?.Invoke(view, null);
                Assert.That(
                    stateField.GetValue(interaction),
                    Is.EqualTo(tradeState),
                    "Switching to Sell must keep the interaction in Trade.");
                Assert.That(buyTab.gameObject.activeSelf, Is.True);
                Assert.That(sellTab.gameObject.activeSelf, Is.True,
                    "Switching to Sell must not collapse the trade navigation.");
                Assert.That(actionTab.gameObject.activeSelf, Is.True);
                Assert.That(buyTab.interactable, Is.True);
                Assert.That(sellTab.interactable, Is.False);

                viewType.GetMethod("ShowNpcBuyList")
                    ?.Invoke(view, null);
                Assert.That(
                    stateField.GetValue(interaction),
                    Is.EqualTo(tradeState),
                    "Switching back to Buy must keep the interaction in Trade.");
                Assert.That(buyTab.gameObject.activeSelf, Is.True);
                Assert.That(sellTab.gameObject.activeSelf, Is.True);
                Assert.That(actionTab.gameObject.activeSelf, Is.True);
                Assert.That(buyTab.interactable, Is.False);
                Assert.That(sellTab.interactable, Is.True);
            }
            finally
            {
                if (interaction != null)
                {
                    Type interactionType = interaction.GetType();
                    interactionType.GetField(
                            "player",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.SetValue(interaction, null);
                    interactionType.GetField(
                            "npc",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.SetValue(interaction, null);
                    interactionType.GetField(
                            "initiator",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.SetValue(interaction, null);
                    interactionType.GetField(
                            "target",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.SetValue(interaction, null);
                    FieldInfo stateField = interactionType.GetField(
                        "<State>k__BackingField",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    stateField?.SetValue(
                        interaction,
                        Enum.Parse(stateField.FieldType, "None"));
                }
                if (interactionHost != null)
                    UnityEngine.Object.DestroyImmediate(interactionHost);
                interactionInstance?.SetValue(null, previousInteraction);
                cardManagerInstance?.SetValue(null, previousCardManager);
                UnityEngine.Object.DestroyImmediate(npcObject);
                UnityEngine.Object.DestroyImmediate(npcDefinition);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void UiRoot_NpcActionRowsUseTextFirstLayoutWithoutRepeatedPortrait()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Transform row = FindChild(
                    instance.transform,
                    "NpcTradeRowTemplate");
                Assert.That(row, Is.Not.Null);

                Component rowView = row.GetComponents<MonoBehaviour>()
                    .First(component => component.GetType().FullName ==
                        "CryingSnow.StackCraft.NpcTradeListRowView");
                MethodInfo bindAction = rowView.GetType().GetMethod(
                    "BindAction",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(bindAction, Is.Not.Null,
                    "Action rows need a dedicated layout instead of reusing merchandise rows.");

                RawImage icon = rowView.GetType().GetField(
                        "icon",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(rowView) as RawImage;
                TMP_Text detailsLabel = rowView.GetType().GetField(
                        "detailsLabel",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(rowView) as TMP_Text;
                Button primaryButton = rowView.GetType().GetField(
                        "primaryButton",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(rowView) as Button;
                TMP_Text primaryButtonLabel = rowView.GetType().GetField(
                        "primaryButtonLabel",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(rowView) as TMP_Text;
                Assert.That(icon, Is.Not.Null);
                Assert.That(detailsLabel, Is.Not.Null);
                Assert.That(primaryButton, Is.Not.Null);
                Assert.That(primaryButtonLabel, Is.Not.Null);
                bool originalAutoSizing =
                    primaryButtonLabel.enableAutoSizing;
                float originalFontSize =
                    primaryButtonLabel.fontSize;

                bindAction.Invoke(
                    rowView,
                    new object[]
                    {
                        "开始互动\n选择一名玩家人物参与",
                        "开始互动",
                        new UnityEngine.Events.UnityAction(() => { })
                    });

                RectTransform details = detailsLabel.rectTransform;
                RectTransform primary =
                    (RectTransform)primaryButton.transform;
                LayoutElement rowLayout = row.GetComponent<LayoutElement>();

                Assert.That(icon.gameObject.activeSelf, Is.False,
                    "The NPC portrait already appears in the header and must not repeat per action.");
                Assert.That(details.anchorMin.x, Is.LessThanOrEqualTo(0.05f));
                Assert.That(details.anchorMax.x, Is.GreaterThanOrEqualTo(0.67f));
                Assert.That(
                    primary.anchorMax.x - primary.anchorMin.x,
                    Is.GreaterThanOrEqualTo(0.24f),
                    "Action buttons need enough width for complete Chinese labels.");
                Assert.That(primaryButtonLabel.enableAutoSizing, Is.True);
                Assert.That(primaryButtonLabel.fontSizeMin, Is.EqualTo(14f));
                Assert.That(primaryButtonLabel.fontSizeMax, Is.EqualTo(20f));
                Assert.That(rowLayout.preferredHeight, Is.LessThanOrEqualTo(96f));

                rowView.GetType().GetMethod(
                        "Bind",
                        BindingFlags.Instance | BindingFlags.Public)
                    ?.Invoke(
                        rowView,
                        new object[]
                        {
                            null,
                            "商品",
                            "购买",
                            new UnityEngine.Events.UnityAction(() => { }),
                            null,
                            null
                        });
                Assert.That(
                    primaryButtonLabel.enableAutoSizing,
                    Is.EqualTo(originalAutoSizing));
                Assert.That(
                    primaryButtonLabel.fontSize,
                    Is.EqualTo(originalFontSize));
                Assert.That(icon.gameObject.activeSelf, Is.True,
                    "Returning to merchandise mode must restore the icon slot.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void UiRoot_BackpackUsesSidebarTabInsteadOfBottomButton()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Transform header = FindChild(
                prefab.transform,
                "Header");
            Transform backpackToggle = FindChild(
                prefab.transform,
                "BackpackToggle");

            Assert.That(header, Is.Not.Null);
            Assert.That(backpackToggle, Is.Not.Null);
            Assert.That(backpackToggle.parent, Is.EqualTo(header));
            Assert.That(FindChild(prefab.transform, "BackpackButton"), Is.Null);
        }

        [Test]
        public void UiRoot_NpcTradeRowsExpandAcrossTheScrollViewport()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Transform content = FindChild(
                prefab.transform,
                "NpcTradeListContent");
            Transform row = FindChild(
                prefab.transform,
                "NpcTradeRowTemplate");
            Transform primaryButton = FindChild(
                row,
                "PrimaryButton");
            Transform secondaryButton = FindChild(
                row,
                "SecondaryButton");
            Assert.That(content, Is.Not.Null);
            Assert.That(row, Is.Not.Null);
            Assert.That(primaryButton, Is.Not.Null);
            Assert.That(secondaryButton, Is.Not.Null);

            VerticalLayoutGroup layout =
                content.GetComponent<VerticalLayoutGroup>();
            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            Assert.That(layout, Is.Not.Null);
            Assert.That(layout.childControlWidth, Is.True,
                "列表必须控制行宽，否则交易按钮会被压缩成竖条。");
            Assert.That(layout.childForceExpandWidth, Is.True);
            Assert.That(rowLayout, Is.Not.Null);
            Assert.That(rowLayout.flexibleWidth, Is.GreaterThan(0f));
            Assert.That(rowLayout.preferredHeight, Is.GreaterThanOrEqualTo(96f));

            var primaryRect = (RectTransform)primaryButton;
            var secondaryRect = (RectTransform)secondaryButton;
            Assert.That(
                primaryRect.anchorMax.x - primaryRect.anchorMin.x,
                Is.GreaterThanOrEqualTo(0.18f));
            Assert.That(
                secondaryRect.anchorMax.x - secondaryRect.anchorMin.x,
                Is.GreaterThanOrEqualTo(0.18f));
            Assert.That(
                primaryButton.GetComponentInChildren<TMP_Text>(true).fontSize,
                Is.LessThanOrEqualTo(20f),
                "窄侧栏里的交易按钮必须使用可完整显示中文的字号。");
            Assert.That(
                secondaryButton.GetComponentInChildren<TMP_Text>(true).fontSize,
                Is.LessThanOrEqualTo(20f));
        }

        [Test]
        public void UiRoot_NpcArtworkPreservesAspectWithoutEscapingItsSlot()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Transform locationArt = FindChild(
                prefab.transform,
                "LocationArt");
            Transform row = FindChild(
                prefab.transform,
                "NpcTradeRowTemplate");
            Transform rowIcon = FindChild(row, "Icon");

            Assert.That(locationArt, Is.Not.Null);
            Assert.That(rowIcon, Is.Not.Null);
            AssertSquareAspectFitter(
                locationArt,
                "人物详情大头像",
                AspectRatioFitter.AspectMode.HeightControlsWidth);
            AssertSquareAspectFitter(
                rowIcon,
                "行动与交易列表小头像",
                AspectRatioFitter.AspectMode.WidthControlsHeight);
        }

        private static void AssertSquareAspectFitter(
            Transform target,
            string label,
            AspectRatioFitter.AspectMode expectedMode)
        {
            AspectRatioFitter fitter =
                target.GetComponent<AspectRatioFitter>();
            Assert.That(
                fitter,
                Is.Not.Null,
                $"{label}必须约束图片宽高比，不能跟随父面板被横向压缩。");
            Assert.That(
                fitter.aspectMode,
                Is.EqualTo(expectedMode),
                $"{label}只能调整自身一个尺寸轴，不能使用会覆盖锚点和位置的 FitInParent。");
            Assert.That(fitter.aspectRatio, Is.EqualTo(1f));
        }

        [Test]
        public void NpcTradeService_DeliversPurchasedItemIntoBackpack()
        {
            Type serviceType =
                FindType("CryingSnow.StackCraft.NpcTradeService");
            Type definitionType =
                FindType("CryingSnow.StackCraft.CardDefinition");
            Type backpackType =
                FindType("CryingSnow.StackCraft.BackpackData");
            MethodInfo deliver = serviceType.GetMethod(
                "TryDeliverPurchasedItem",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { definitionType, backpackType },
                null);
            Assert.That(deliver, Is.Not.Null,
                "购买商品必须有明确的背包交付步骤。");

            ScriptableObject product = CreateDefinition(
                "test-purchased-item",
                3,
                true,
                1,
                false);
            object backpack = Activator.CreateInstance(backpackType);
            try
            {
                Assert.That(
                    deliver.Invoke(null, new[] { product, backpack }),
                    Is.True);
                System.Collections.IList entries =
                    (System.Collections.IList)backpackType
                        .GetField("Entries").GetValue(backpack);
                Assert.That(entries.Count, Is.EqualTo(1));
                object cardData = entries[0].GetType()
                    .GetField("Card").GetValue(entries[0]);
                Assert.That(
                    cardData.GetType().GetField("Id").GetValue(cardData),
                    Is.EqualTo("test-purchased-item"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(product);
            }
        }

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
            Assert.That(buyer.objectReferenceValue, Is.Null,
                "NPC 通用交易后不应再配置独立收购台。");
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
        public void WorldMapLocationView_ShowBuilding_ReopensHiddenBodyWhenTabIsAlreadySelected()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/StackCraft/Scenes/Location.unity",
                OpenSceneMode.Additive);
            GameObject buildingObject = new("Building View Reopen Test");
            ScriptableObject buildingDefinition = CreateDefinition(
                "test-building-view-reopen",
                6,
                false,
                0,
                true);
            try
            {
                Type viewType =
                    FindType("CryingSnow.StackCraft.WorldMapLocationView");
                Type cardType =
                    FindType("CryingSnow.StackCraft.CardInstance");
                Type stackType =
                    FindType("CryingSnow.StackCraft.CardStack");
                Type entranceType =
                    FindType("CryingSnow.StackCraft.LocationEntrance");
                Component view = scene.GetRootGameObjects()
                    .SelectMany(root =>
                        root.GetComponentsInChildren<Component>(true))
                    .FirstOrDefault(component =>
                        component != null &&
                        component.GetType() == viewType);
                Assert.That(view, Is.Not.Null);

                Component card = buildingObject.AddComponent(cardType);
                SetDefinition(card, buildingDefinition);
                cardType.GetProperty("Stack")?.SetValue(
                    card,
                    Activator.CreateInstance(
                        stackType,
                        card,
                        Vector3.zero));
                Component entrance =
                    buildingObject.AddComponent(entranceType);
                entranceType.GetMethod("Configure")
                    ?.Invoke(entrance, new object[] { "test-destination" });

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

                viewType.GetMethod(
                        "ShowBuilding",
                        new[] { entranceType })
                    ?.Invoke(view, new[] { entrance });

                Assert.That(canvasGroup.alpha, Is.EqualTo(1f),
                    "Switching from an NPC to a building must restore the sidebar body even when the tab remains selected.");
                Assert.That(canvasGroup.interactable, Is.True);
                Assert.That(canvasGroup.blocksRaycasts, Is.True);
                TMP_Text title = viewType.GetField(
                        "titleLabel",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(view) as TMP_Text;
                Assert.That(title.text, Is.EqualTo("test-building-view-reopen"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(buildingObject);
                UnityEngine.Object.DestroyImmediate(buildingDefinition);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void UiRoot_HidesRecipesModuleUntilItIsReady()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/StackCraft/Prefabs/UI/UIRoot.prefab");
            Transform recipesToggle = FindChild(
                prefab.transform,
                "RecipesToggle");
            Transform recipesView = FindChild(
                prefab.transform,
                "RecipesView");

            Assert.That(recipesToggle, Is.Not.Null);
            Assert.That(recipesView, Is.Not.Null);
            Assert.That(recipesToggle.gameObject.activeSelf, Is.False,
                "The unfinished recipes tab should not appear in the sidebar header.");
            Assert.That(recipesView.gameObject.activeSelf, Is.False,
                "The unfinished recipes body should remain unavailable.");
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

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;

            foreach (Transform child in root)
            {
                Transform found = FindChild(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static object CreateCardData(Type cardDataType, string id)
        {
            object cardData = Activator.CreateInstance(cardDataType);
            cardDataType.GetField("Id").SetValue(cardData, id);
            return cardData;
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
