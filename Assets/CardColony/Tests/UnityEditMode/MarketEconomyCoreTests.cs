using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CardColony.Tests
{
    public sealed class MarketEconomyCoreTests
    {
        [Test]
        public void QuoteCalculator_RespondsToStockAndPreventsLocalArbitrage()
        {
            Type commodityType = FindType(
                "CryingSnow.StackCraft.CommodityDefinition");
            Type profileType = FindType(
                "CryingSnow.StackCraft.MarketProfile");
            Type ruleType = FindType(
                "CryingSnow.StackCraft.MarketCommodityRule");
            Type stateType = FindType(
                "CryingSnow.StackCraft.MarketStateData");
            Type commodityStateType = FindType(
                "CryingSnow.StackCraft.MarketCommodityStateData");
            Type modifiersType = FindType(
                "CryingSnow.StackCraft.MerchantPriceModifiers");
            Type calculatorType = FindType(
                "CryingSnow.StackCraft.MarketQuoteCalculator");

            Assert.That(commodityType, Is.Not.Null,
                "P0 商品定义尚未实现。");
            Assert.That(profileType, Is.Not.Null,
                "P0 地区市场定义尚未实现。");
            Assert.That(calculatorType, Is.Not.Null,
                "P0 统一报价器尚未实现。");

            ScriptableObject commodity =
                ScriptableObject.CreateInstance(commodityType);
            ScriptableObject profile =
                ScriptableObject.CreateInstance(profileType);
            try
            {
                SetField(commodity, "id", "food");
                SetField(commodity, "basePrice", 10);
                SetField(commodity, "volatility", 0.5f);
                SetField(profile, "id", "test-market");
                SetField(profile, "baseSpread", 0.2f);

                object rule = Activator.CreateInstance(ruleType);
                SetField(rule, "commodity", commodity);
                SetField(rule, "regionalPriceFactor", 1f);
                SetField(rule, "targetStock", 10);
                SetField(rule, "allowsPlayerPurchase", true);
                SetField(rule, "allowsPlayerSale", true);

                object state = Activator.CreateInstance(stateType);
                SetPublicField(state, "MarketId", "test-market");
                SetPublicField(state, "AvailableFunds", 100);
                SetPublicField(state, "StateRevision", 3);
                object commodityState =
                    Activator.CreateInstance(commodityStateType);
                SetPublicField(commodityState, "CommodityId", "food");
                SetPublicField(commodityState, "Stock", 10);
                ((IList)GetPublicField(state, "Commodities"))
                    .Add(commodityState);

                object calculator = Activator.CreateInstance(calculatorType);
                object modifiers = Activator.CreateInstance(modifiersType);
                MethodInfo calculate = calculatorType.GetMethod(
                    "Calculate",
                    BindingFlags.Public | BindingFlags.Instance);
                Assert.That(calculate, Is.Not.Null);

                object normal = calculate.Invoke(
                    calculator,
                    new[]
                    {
                        profile,
                        rule,
                        state,
                        modifiers,
                        (object)24L,
                        1f
                    });
                int normalBuy = GetIntProperty(
                    normal,
                    "PlayerBuyUnitPrice");
                int normalSell = GetIntProperty(
                    normal,
                    "PlayerSellUnitPrice");
                Assert.That(normalBuy, Is.GreaterThan(normalSell),
                    "同地买入后立即卖出必须亏损。");

                SetPublicField(commodityState, "Stock", 2);
                object shortage = calculate.Invoke(
                    calculator,
                    new[]
                    {
                        profile,
                        rule,
                        state,
                        modifiers,
                        (object)24L,
                        1f
                    });
                Assert.That(
                    GetIntProperty(shortage, "PlayerBuyUnitPrice"),
                    Is.GreaterThan(normalBuy));
                Assert.That(
                    GetIntProperty(shortage, "PlayerSellUnitPrice"),
                    Is.GreaterThan(normalSell));

                SetPublicField(commodityState, "Stock", 20);
                object abundant = calculate.Invoke(
                    calculator,
                    new[]
                    {
                        profile,
                        rule,
                        state,
                        modifiers,
                        (object)24L,
                        1f
                    });
                Assert.That(
                    GetIntProperty(abundant, "PlayerBuyUnitPrice"),
                    Is.LessThan(normalBuy));
                Assert.That(
                    GetIntProperty(abundant, "PlayerSellUnitPrice"),
                    Is.LessThan(normalSell));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(commodity);
            }
        }

        [Test]
        [Timeout(5000)]
        public void RefreshEngine_IsDeterministicIdempotentAndBounded()
        {
            Type engineType = FindType(
                "CryingSnow.StackCraft.MarketRefreshEngine");
            Assert.That(engineType, Is.Not.Null,
                "P0 确定性市场刷新器尚未实现。");

            object setup = CreateMarketSetup(
                "refresh-market",
                "wood",
                5,
                1f,
                8,
                12);
            ScriptableObject profile =
                (ScriptableObject)GetTupleValue(setup, "Profile");
            ScriptableObject commodity =
                (ScriptableObject)GetTupleValue(setup, "Commodity");
            object first = Activator.CreateInstance(
                FindType("CryingSnow.StackCraft.MarketStateData"));
            object second = Activator.CreateInstance(
                FindType("CryingSnow.StackCraft.MarketStateData"));
            object engine = Activator.CreateInstance(engineType);
            try
            {
                MethodInfo initialize = engineType.GetMethod("Initialize");
                MethodInfo catchUp = engineType.GetMethod("CatchUp");
                initialize.Invoke(
                    engine,
                    new[] { profile, first, (object)0L, 417 });
                initialize.Invoke(
                    engine,
                    new[] { profile, second, (object)0L, 417 });

                Assert.That(
                    GetPublicField(first, "NextRefreshWorldHour"),
                    Is.EqualTo(
                        GetPublicField(second, "NextRefreshWorldHour")));
                int revision = (int)GetPublicField(
                    first,
                    "StateRevision");
                long next = (long)GetPublicField(
                    first,
                    "NextRefreshWorldHour");
                catchUp.Invoke(
                    engine,
                    new[] { profile, first, (object)(next - 1), 417 });
                Assert.That(
                    GetPublicField(first, "StateRevision"),
                    Is.EqualTo(revision),
                    "未到刷新节点时不能改变状态。");

                catchUp.Invoke(
                    engine,
                    new[] { profile, first, (object)next, 417 });
                catchUp.Invoke(
                    engine,
                    new[] { profile, second, (object)next, 417 });
                Assert.That(
                    Snapshot(first),
                    Is.EqualTo(Snapshot(second)),
                    "相同种子与状态必须产生相同刷新结果。");

                string refreshed = Snapshot(first);
                catchUp.Invoke(
                    engine,
                    new[] { profile, first, (object)next, 417 });
                Assert.That(
                    Snapshot(first),
                    Is.EqualTo(refreshed),
                    "同一时刻重复访问不能重抽行情。");

                object oneShot = Activator.CreateInstance(
                    FindType(
                        "CryingSnow.StackCraft.MarketStateData"));
                object chunked = Activator.CreateInstance(
                    FindType(
                        "CryingSnow.StackCraft.MarketStateData"));
                initialize.Invoke(
                    engine,
                    new[] { profile, oneShot, (object)0L, 417 });
                initialize.Invoke(
                    engine,
                    new[] { profile, chunked, (object)0L, 417 });
                long finalHour = 0L;
                for (int round = 0; round < 100; round++)
                {
                    finalHour = (long)GetPublicField(
                        chunked,
                        "NextRefreshWorldHour");
                    catchUp.Invoke(
                        engine,
                        new[] { profile, chunked, (object)finalHour, 417 });
                }
                catchUp.Invoke(
                    engine,
                    new[] { profile, oneShot, (object)finalHour, 417 });
                int maxRefreshesPerCatchUp = (int)engineType
                    .GetField("MaxRefreshesPerCatchUp")
                    .GetRawConstantValue();
                Assert.That(
                    GetIntProperty(engine, "LastExplicitRefreshCount"),
                    Is.LessThanOrEqualTo(maxRefreshesPerCatchUp),
                    "单次离线补算必须有固定工作预算，避免异常存档阻塞主线程。");
                Assert.That(
                    (long)GetPublicField(
                        oneShot,
                        "NextRefreshWorldHour"),
                    Is.GreaterThan(finalHour),
                    "超过显式预算的刷新必须在同一次调用中聚合补完。");
                Assert.That(
                    Snapshot(oneShot),
                    Is.EqualTo(Snapshot(chunked)),
                    "长时间离线一次补算与分段访问必须得到相同状态。");

                string completedCatchUp = Snapshot(oneShot);
                catchUp.Invoke(
                    engine,
                    new[] { profile, oneShot, (object)finalHour, 417 });
                Assert.That(
                    Snapshot(oneShot),
                    Is.EqualTo(completedCatchUp),
                    "同一世界时刻重复调用不得继续改变已补完的行情。");

                object extreme = Activator.CreateInstance(
                    FindType(
                        "CryingSnow.StackCraft.MarketStateData"));
                initialize.Invoke(
                    engine,
                    new[]
                    {
                        profile,
                        extreme,
                        (object)(long.MaxValue - 1),
                        417
                    });
                string extremeBefore = Snapshot(extreme);
                catchUp.Invoke(
                    engine,
                    new[]
                    {
                        profile,
                        extreme,
                        (object)long.MaxValue,
                        417
                    });
                Assert.That(
                    Snapshot(extreme),
                    Is.EqualTo(extremeBefore),
                    "long 边界不得发生时间溢出或无限补算。");

                SetPublicField(
                    extreme,
                    "RefreshSequence",
                    int.MaxValue);
                SetPublicField(
                    extreme,
                    "NextRefreshWorldHour",
                    0L);
                string sequenceBoundary = Snapshot(extreme);
                catchUp.Invoke(
                    engine,
                    new[]
                    {
                        profile,
                        extreme,
                        (object)long.MaxValue,
                        417
                    });
                Assert.That(
                    Snapshot(extreme),
                    Is.EqualTo(sequenceBoundary),
                    "刷新序号到达 int 边界后必须安全停止。");

                SetPublicField(
                    extreme,
                    "RefreshSequence",
                    0);
                SetPublicField(
                    extreme,
                    "StateRevision",
                    int.MaxValue);
                string revisionBoundary = Snapshot(extreme);
                catchUp.Invoke(
                    engine,
                    new[]
                    {
                        profile,
                        extreme,
                        (object)long.MaxValue,
                        417
                    });
                Assert.That(
                    Snapshot(extreme),
                    Is.EqualTo(revisionBoundary),
                    "修订号到达 int 边界后不得继续改变行情。");

                object hugeGap = Activator.CreateInstance(
                    FindType(
                        "CryingSnow.StackCraft.MarketStateData"));
                initialize.Invoke(
                    engine,
                    new[] { profile, hugeGap, (object)0L, 417 });
                catchUp.Invoke(
                    engine,
                    new[]
                    {
                        profile,
                        hugeGap,
                        (object)long.MaxValue,
                        417
                    });
                Assert.That(
                    GetPublicField(hugeGap, "RefreshSequence"),
                    Is.EqualTo(int.MaxValue));
                Assert.That(
                    GetPublicField(hugeGap, "StateRevision"),
                    Is.EqualTo(int.MaxValue));
                string exhaustedGap = Snapshot(hugeGap);
                catchUp.Invoke(
                    engine,
                    new[]
                    {
                        profile,
                        hugeGap,
                        (object)long.MaxValue,
                        417
                    });
                Assert.That(
                    Snapshot(hugeGap),
                    Is.EqualTo(exhaustedGap),
                    "极端离线时长必须有界完成且重复访问稳定。");

                MethodInfo applyRepeatedCycle =
                    engineType.GetMethod(
                        "ApplyRepeatedCycle",
                        BindingFlags.NonPublic |
                        BindingFlags.Static);
                Assert.That(
                    applyRepeatedCycle.Invoke(
                        null,
                        new object[]
                        {
                            0,
                            10L,
                            50,
                            100,
                            2L
                        }),
                    Is.EqualTo(60),
                    "周期聚合必须精确保留逐轮 Clamp 的结果。");
                Assert.That(
                    applyRepeatedCycle.Invoke(
                        null,
                        new object[]
                        {
                            100,
                            -10L,
                            0,
                            50,
                            2L
                        }),
                    Is.EqualTo(40));
                Assert.That(
                    applyRepeatedCycle.Invoke(
                        null,
                        new object[]
                        {
                            60,
                            49L,
                            49,
                            60,
                            2L
                        }),
                    Is.EqualTo(60),
                    "周期步长跨过整个库存区间时不得突破最大库存。");
                Assert.That(
                    applyRepeatedCycle.Invoke(
                        null,
                        new object[]
                        {
                            0,
                            -49L,
                            0,
                            11,
                            2L
                        }),
                    Is.EqualTo(0),
                    "负周期跨界时不得跌破最小库存。");
                var cycleOracleRandom = new System.Random(417);
                for (int sample = 0; sample < 250; sample++)
                {
                    int mappedMinimum =
                        cycleOracleRandom.Next(0, 50);
                    int mappedMaximum =
                        cycleOracleRandom.Next(
                            mappedMinimum,
                            80);
                    long cycleDelta =
                        cycleOracleRandom.Next(-100, 101);
                    int initialStock =
                        cycleOracleRandom.Next(0, 80);
                    long cycleCount =
                        cycleOracleRandom.Next(1, 80);
                    int expectedStock = initialStock;
                    for (int cycle = 0;
                         cycle < cycleCount;
                         cycle++)
                    {
                        expectedStock = (int)Math.Max(
                            mappedMinimum,
                            Math.Min(
                                mappedMaximum,
                                expectedStock + cycleDelta));
                    }
                    object aggregatedStock =
                        applyRepeatedCycle.Invoke(
                            null,
                            new object[]
                            {
                                initialStock,
                                cycleDelta,
                                mappedMinimum,
                                mappedMaximum,
                                cycleCount
                            });
                    Assert.That(
                        aggregatedStock,
                        Is.EqualTo(expectedStock),
                        $"周期聚合与逐轮 oracle 不一致：" +
                        $"stock={initialStock}, delta={cycleDelta}, " +
                        $"range={mappedMinimum}..{mappedMaximum}, " +
                        $"count={cycleCount}");
                }

                object commodityState =
                    ((IList)GetPublicField(first, "Commodities"))[0];
                int stock = (int)GetPublicField(
                    commodityState,
                    "Stock");
                Assert.That(stock, Is.InRange(0, 20));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(commodity);
            }
        }

        [Test]
        public void MarketService_TradesAtomicallyAndRejectsStaleQuote()
        {
            Type catalogType = FindType(
                "CryingSnow.StackCraft.InMemoryMarketCatalog");
            Type repositoryType = FindType(
                "CryingSnow.StackCraft.InMemoryMarketStateRepository");
            Type clockType = FindType(
                "CryingSnow.StackCraft.MutableWorldClock");
            Type inventoryType = FindType(
                "CryingSnow.StackCraft.InMemoryPlayerTradeInventory");
            Type serviceType = FindType(
                "CryingSnow.StackCraft.MarketService");
            Type requestType = FindType(
                "CryingSnow.StackCraft.MarketTradeRequest");
            Type directionType = FindType(
                "CryingSnow.StackCraft.MarketTradeDirection");
            Assert.That(serviceType, Is.Not.Null,
                "P0 原子市场服务尚未实现。");
            Assert.That(inventoryType, Is.Not.Null,
                "P0 内存玩家资产适配器尚未实现。");
            Type atomicInventoryType = FindType(
                "CryingSnow.StackCraft.IAtomicPlayerTradeInventory");
            Assert.That(atomicInventoryType, Is.Not.Null,
                "P0 交易需要显式的原子玩家资产提交接口。");
            Assert.That(
                atomicInventoryType.IsAssignableFrom(inventoryType),
                Is.True);

            object setup = CreateMarketSetup(
                "atomic-market",
                "food",
                4,
                1f,
                10,
                10);
            ScriptableObject profile =
                (ScriptableObject)GetTupleValue(setup, "Profile");
            ScriptableObject commodity =
                (ScriptableObject)GetTupleValue(setup, "Commodity");
            try
            {
                object catalog = Activator.CreateInstance(
                    catalogType,
                    new object[]
                    {
                        CreateTypedArray(
                            commodity.GetType(),
                            commodity),
                        CreateTypedArray(profile.GetType(), profile)
                    });
                object repository =
                    Activator.CreateInstance(repositoryType);
                object clock =
                    Activator.CreateInstance(clockType, 0L);
                object service = Activator.CreateInstance(
                    serviceType,
                    catalog,
                    clock,
                    repository,
                    99);
                Type filterType = FindType(
                    "CryingSnow.StackCraft.MerchantTradeFilter");
                object defaultModifiers = Activator.CreateInstance(
                    FindType(
                        "CryingSnow.StackCraft.MerchantPriceModifiers"));
                MethodInfo registerPolicy = serviceType.GetMethod(
                    "RegisterMerchantPolicy");
                Assert.That(registerPolicy, Is.Not.Null,
                    "商人经营范围必须由市场服务的可信策略注册，而不是由请求自带。");
                registerPolicy.Invoke(
                    service,
                    new[]
                    {
                        "merchant",
                        Activator.CreateInstance(
                            filterType,
                            new object[] { null, null }),
                        defaultModifiers
                    });
                object inventory = Activator.CreateInstance(
                    inventoryType,
                    100,
                    new Dictionary<string, int>(),
                    20);

                object quote = serviceType.GetMethod("GetQuote").Invoke(
                    service,
                    new object[]
                    {
                        "atomic-market",
                        "food",
                        Activator.CreateInstance(
                            FindType(
                                "CryingSnow.StackCraft.MerchantPriceModifiers"))
                    });
                int buyPrice = GetIntProperty(
                    quote,
                    "PlayerBuyUnitPrice");
                int revision = GetIntProperty(quote, "StateRevision");
                object request = Activator.CreateInstance(
                    requestType,
                    "atomic-market",
                    "merchant",
                    "food",
                    Enum.Parse(directionType, "PlayerBuys"),
                    2,
                    buyPrice,
                    revision);
                object result = serviceType.GetMethod("Execute").Invoke(
                    service,
                    new[] { request, inventory });
                Assert.That(GetBoolProperty(result, "Success"), Is.True);
                Assert.That(
                    GetIntProperty(inventory, "Currency"),
                    Is.EqualTo(100 - buyPrice * 2));
                Assert.That(
                    (int)inventoryType.GetMethod("CountCommodity").Invoke(
                        inventory,
                        new object[] { "food" }),
                    Is.EqualTo(2));

                string beforeDuplicate = InventorySnapshot(inventory);
                object stale = serviceType.GetMethod("Execute").Invoke(
                    service,
                    new[] { request, inventory });
                Assert.That(GetBoolProperty(stale, "Success"), Is.False);
                Assert.That(
                    InventorySnapshot(inventory),
                    Is.EqualTo(beforeDuplicate),
                    "过期报价失败时玩家资产不得发生变化。");

                PropertyInfo failAddCurrency =
                    inventoryType.GetProperty("FailNextAddCurrency");
                Assert.That(failAddCurrency, Is.Not.Null,
                    "测试资产适配器需要覆盖提交失败回滚。");
                object sellQuote = serviceType.GetMethod("GetQuote").Invoke(
                    service,
                    new object[]
                    {
                        "atomic-market",
                        "food",
                        Activator.CreateInstance(
                            FindType(
                                "CryingSnow.StackCraft.MerchantPriceModifiers"))
                    });
                object sellRequest = Activator.CreateInstance(
                    requestType,
                    "atomic-market",
                    "merchant",
                    "food",
                    Enum.Parse(directionType, "PlayerSells"),
                    1,
                    GetIntProperty(
                        sellQuote,
                        "PlayerSellUnitPrice"),
                    GetIntProperty(sellQuote, "StateRevision"));
                string inventoryBeforeRollback =
                    InventorySnapshot(inventory);
                string marketBeforeRollback = MarketSnapshot(
                    serviceType.GetMethod("GetOrCreateState").Invoke(
                        service,
                        new object[] { "atomic-market" }));
                failAddCurrency.SetValue(inventory, true);
                object rolledBack = serviceType.GetMethod("Execute").Invoke(
                    service,
                    new[] { sellRequest, inventory });
                Assert.That(
                    GetBoolProperty(rolledBack, "Success"),
                    Is.False);
                Assert.That(
                    InventorySnapshot(inventory),
                    Is.EqualTo(inventoryBeforeRollback));
                Assert.That(
                    MarketSnapshot(
                        serviceType.GetMethod("GetOrCreateState").Invoke(
                            service,
                            new object[] { "atomic-market" })),
                    Is.EqualTo(marketBeforeRollback),
                    "提交失败必须完整回滚玩家资产与市场状态。");

                PropertyInfo throwAfterMutation =
                    inventoryType.GetProperty(
                        "ThrowAfterMutationOnNextCommit");
                Assert.That(throwAfterMutation, Is.Not.Null,
                    "测试适配器必须覆盖玩家资产已修改后通知抛异常的回滚路径。");
                object exceptionQuote = serviceType.GetMethod(
                    "GetQuote").Invoke(
                    service,
                    new object[]
                    {
                        "atomic-market",
                        "food",
                        defaultModifiers
                    });
                object exceptionRequest = Activator.CreateInstance(
                    requestType,
                    "atomic-market",
                    "merchant",
                    "food",
                    Enum.Parse(directionType, "PlayerBuys"),
                    1,
                    GetIntProperty(
                        exceptionQuote,
                        "PlayerBuyUnitPrice"),
                    GetIntProperty(
                        exceptionQuote,
                        "StateRevision"));
                string inventoryBeforeException =
                    InventorySnapshot(inventory);
                string marketBeforeException = MarketSnapshot(
                    serviceType.GetMethod("GetOrCreateState").Invoke(
                        service,
                        new object[] { "atomic-market" }));
                throwAfterMutation.SetValue(inventory, true);
                object exceptionResult = serviceType.GetMethod(
                    "Execute").Invoke(
                    service,
                    new[] { exceptionRequest, inventory });
                Assert.That(
                    GetBoolProperty(exceptionResult, "Success"),
                    Is.False);
                Assert.That(
                    InventorySnapshot(inventory),
                    Is.EqualTo(inventoryBeforeException));
                Assert.That(
                    MarketSnapshot(
                        serviceType.GetMethod("GetOrCreateState").Invoke(
                            service,
                            new object[] { "atomic-market" })),
                    Is.EqualTo(marketBeforeException),
                    "玩家资产通知抛异常时，玩家与市场状态都必须回滚。");

                object poorInventory = Activator.CreateInstance(
                    inventoryType,
                    0,
                    new Dictionary<string, int>(),
                    20);
                object latestQuote = serviceType.GetMethod("GetQuote").Invoke(
                    service,
                    new object[]
                    {
                        "atomic-market",
                        "food",
                        Activator.CreateInstance(
                            FindType(
                                "CryingSnow.StackCraft.MerchantPriceModifiers"))
                    });
                object poorRequest = Activator.CreateInstance(
                    requestType,
                    "atomic-market",
                    "merchant",
                    "food",
                    Enum.Parse(directionType, "PlayerBuys"),
                    1,
                    GetIntProperty(latestQuote, "PlayerBuyUnitPrice"),
                    GetIntProperty(latestQuote, "StateRevision"));
                string marketBefore = MarketSnapshot(
                    serviceType.GetMethod("GetOrCreateState").Invoke(
                        service,
                        new object[] { "atomic-market" }));
                object rejected = serviceType.GetMethod("Execute").Invoke(
                    service,
                    new[] { poorRequest, poorInventory });
                Assert.That(GetBoolProperty(rejected, "Success"), Is.False);
                Assert.That(
                    MarketSnapshot(
                        serviceType.GetMethod("GetOrCreateState").Invoke(
                            service,
                            new object[] { "atomic-market" })),
                    Is.EqualTo(marketBefore),
                    "金币不足时市场状态不得发生变化。");

                object boundedState =
                    serviceType.GetMethod("GetOrCreateState").Invoke(
                        service,
                        new object[] { "atomic-market" });
                object boundedCommodity =
                    ((IList)GetPublicField(
                        boundedState,
                        "Commodities"))[0];
                SetPublicField(boundedCommodity, "Stock", 20);
                SetPublicField(boundedState, "AvailableFunds", 400);
                object fullInventory = Activator.CreateInstance(
                    inventoryType,
                    100,
                    new Dictionary<string, int>
                    {
                        ["food"] = 1
                    },
                    20);
                object boundedQuote =
                    serviceType.GetMethod("GetQuote").Invoke(
                        service,
                        new object[]
                        {
                            "atomic-market",
                            "food",
                            defaultModifiers
                        });
                object overstockRequest = Activator.CreateInstance(
                    requestType,
                    "atomic-market",
                    "merchant",
                    "food",
                    Enum.Parse(directionType, "PlayerSells"),
                    1,
                    GetIntProperty(
                        boundedQuote,
                        "PlayerSellUnitPrice"),
                    GetIntProperty(boundedQuote, "StateRevision"));
                string beforeOverstock = MarketSnapshot(boundedState);
                object overstockResult =
                    serviceType.GetMethod("Execute").Invoke(
                        service,
                        new[] { overstockRequest, fullInventory });
                Assert.That(
                    GetBoolProperty(overstockResult, "Success"),
                    Is.False);
                Assert.That(
                    MarketSnapshot(boundedState),
                    Is.EqualTo(beforeOverstock),
                    "交易不能让库存超过商品配置上限。");

                SetPublicField(boundedCommodity, "Stock", 10);
                boundedQuote = serviceType.GetMethod("GetQuote").Invoke(
                    service,
                    new object[]
                    {
                        "atomic-market",
                        "food",
                        defaultModifiers
                    });
                object fundCapRequest = Activator.CreateInstance(
                    requestType,
                    "atomic-market",
                    "merchant",
                    "food",
                    Enum.Parse(directionType, "PlayerBuys"),
                    1,
                    GetIntProperty(
                        boundedQuote,
                        "PlayerBuyUnitPrice"),
                    GetIntProperty(boundedQuote, "StateRevision"));
                string beforeFundCap = MarketSnapshot(boundedState);
                object fundCapResult =
                    serviceType.GetMethod("Execute").Invoke(
                        service,
                        new[] { fundCapRequest, fullInventory });
                Assert.That(
                    GetBoolProperty(fundCapResult, "Success"),
                    Is.False);
                Assert.That(
                    MarketSnapshot(boundedState),
                    Is.EqualTo(beforeFundCap),
                    "交易不能让市场资金超过配置上限。");

                object rogueRequest = Activator.CreateInstance(
                    requestType,
                    "atomic-market",
                    "rogue",
                    "food",
                    Enum.Parse(directionType, "PlayerBuys"),
                    1,
                    GetIntProperty(
                        boundedQuote,
                        "PlayerBuyUnitPrice"),
                    GetIntProperty(boundedQuote, "StateRevision"));
                object rogueResult =
                    serviceType.GetMethod("Execute").Invoke(
                        service,
                        new[] { rogueRequest, fullInventory });
                foreach (string invalidMerchantId in new[]
                         {
                             null,
                             string.Empty,
                             "   "
                         })
                {
                    object invalidMerchantRequest =
                        Activator.CreateInstance(
                            requestType,
                            "atomic-market",
                            invalidMerchantId,
                            "food",
                            Enum.Parse(directionType, "PlayerBuys"),
                            1,
                            GetIntProperty(
                                boundedQuote,
                                "PlayerBuyUnitPrice"),
                            GetIntProperty(
                                boundedQuote,
                                "StateRevision"));
                    object invalidMerchantResult =
                        serviceType.GetMethod("Execute").Invoke(
                            service,
                            new[]
                            {
                                invalidMerchantRequest,
                                fullInventory
                            });
                    Assert.That(
                        GetBoolProperty(
                            invalidMerchantResult,
                            "Success"),
                        Is.False,
                        "空商人 ID 不能绕过可信经营策略。");
                }

                SetPublicField(
                    boundedState,
                    "AvailableFunds",
                    100);
                SetPublicField(
                    boundedState,
                    "StateRevision",
                    int.MaxValue);
                object exhaustedQuote =
                    serviceType.GetMethod("GetQuote").Invoke(
                        service,
                        new object[]
                        {
                            "atomic-market",
                            "food",
                            defaultModifiers
                        });
                object exhaustedRequest = Activator.CreateInstance(
                    requestType,
                    "atomic-market",
                    "merchant",
                    "food",
                    Enum.Parse(directionType, "PlayerBuys"),
                    1,
                    GetIntProperty(
                        exhaustedQuote,
                        "PlayerBuyUnitPrice"),
                    int.MaxValue);
                string exhaustedInventory =
                    InventorySnapshot(fullInventory);
                string exhaustedMarket =
                    MarketSnapshot(boundedState);
                object exhaustedResult =
                    serviceType.GetMethod("Execute").Invoke(
                        service,
                        new[] { exhaustedRequest, fullInventory });
                Assert.That(
                    GetBoolProperty(exhaustedResult, "Success"),
                    Is.False);
                Assert.That(
                    InventorySnapshot(fullInventory),
                    Is.EqualTo(exhaustedInventory));
                Assert.That(
                    MarketSnapshot(boundedState),
                    Is.EqualTo(exhaustedMarket),
                    "修订号耗尽后交易必须拒绝，不能溢出或复用旧报价。");

                Assert.That(
                    GetBoolProperty(rogueResult, "Success"),
                    Is.False,
                    "未注册商人不能通过省略请求过滤器绕过经营范围。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(commodity);
            }
        }

        [Test]
        public void EconomyAssets_DefineThreeMarketsEightCommoditiesAndRoutes()
        {
            string commodityFolder =
                "Assets/StackCraft/Resources/Trading/Commodities";
            string marketFolder =
                "Assets/StackCraft/Resources/Trading/Markets";
            string[] commodityGuids = AssetDatabase.FindAssets(
                "t:CommodityDefinition",
                new[] { commodityFolder });
            string[] marketGuids = AssetDatabase.FindAssets(
                "t:MarketProfile",
                new[] { marketFolder });
            Assert.That(commodityGuids.Length, Is.EqualTo(8),
                "P0 必须提供八种数据级贸易商品。");
            Assert.That(marketGuids.Length, Is.EqualTo(3),
                "P0 必须提供河湾、白石城和旧矿营地三个数据级市场。");

            var commodities = commodityGuids
                .Select(guid => AssetDatabase.LoadAssetAtPath<
                    ScriptableObject>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .ToDictionary(
                    asset => new SerializedObject(asset)
                        .FindProperty("id").stringValue);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "food", "wood", "salt", "cloth",
                    "tools", "ore", "medicine", "rope"
                },
                commodities.Keys);
            foreach (ScriptableObject commodity in commodities.Values)
            {
                Assert.That(
                    new SerializedObject(commodity)
                        .FindProperty("cardDefinition")
                        .objectReferenceValue,
                    Is.Not.Null,
                    $"{commodity.name} 缺少可实际搬运的卡牌载体。");
            }

            var markets = marketGuids
                .Select(guid => AssetDatabase.LoadAssetAtPath<
                    ScriptableObject>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .ToDictionary(
                    asset => new SerializedObject(asset)
                        .FindProperty("id").stringValue);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "riverbend-market",
                    "whitestone-market",
                    "old-mine-market"
                },
                markets.Keys);

            int riverbendFood = AssetQuote(
                markets["riverbend-market"],
                "food",
                20,
                "PlayerBuyUnitPrice");
            int oldMineFoodSale = AssetQuote(
                markets["old-mine-market"],
                "food",
                20,
                "PlayerSellUnitPrice");
            int oldMineOre = AssetQuote(
                markets["old-mine-market"],
                "ore",
                20,
                "PlayerBuyUnitPrice");
            int whitestoneOreSale = AssetQuote(
                markets["whitestone-market"],
                "ore",
                20,
                "PlayerSellUnitPrice");
            Assert.That(riverbendFood, Is.LessThan(oldMineFoodSale),
                "河湾粮食必须比旧矿营地便宜，形成跑商路线。");
            Assert.That(oldMineOre, Is.LessThan(whitestoneOreSale),
                "旧矿矿石必须比白石城便宜，形成跑商路线。");
            double foodNetMargin =
                (oldMineFoodSale - riverbendFood - 1d) /
                riverbendFood;
            double oreNetMargin =
                (whitestoneOreSale - oldMineOre - 1d) /
                oldMineOre;
            Assert.That(foodNetMargin, Is.InRange(.15d, .35d));
            Assert.That(oreNetMargin, Is.InRange(.15d, .35d));
            AssertRouteMarginAcrossStockRange(
                markets["riverbend-market"],
                markets["old-mine-market"],
                "food");
            AssertRouteMarginAcrossStockRange(
                markets["old-mine-market"],
                markets["whitestone-market"],
                "ore");

            AssertTenRefreshesStayValid(markets.Values);
        }

        [Test]
        public void SaveAndNpcProfiles_ExposeP0EconomyState()
        {
            Type gameDataType = FindType(
                "CryingSnow.StackCraft.GameData");
            foreach (string fieldName in new[]
                     {
                         "EconomyStateVersion",
                         "EconomySeed",
                         "WorldElapsedHours",
                         "Markets"
                     })
            {
                Assert.That(
                    gameDataType.GetField(fieldName),
                    Is.Not.Null,
                    $"GameData 缺少 P0 字段 {fieldName}");
            }

            Type profileType = FindType(
                "CryingSnow.StackCraft.NpcTradeProfile");
            foreach (string fieldName in new[]
                     {
                         "marketProfile",
                         "sellCommodityTags",
                         "buyCommodityTags",
                         "usesMarketFunds",
                         "personalFundLimit"
                     })
            {
                Assert.That(
                    profileType.GetField(
                        fieldName,
                        BindingFlags.Instance |
                        BindingFlags.NonPublic),
                    Is.Not.Null,
                    $"NpcTradeProfile 缺少 P0 字段 {fieldName}");
            }
        }

        [Test]
        public void NpcTradeFacade_ExposesRegionalQuoteToUiAndExecution()
        {
            Type serviceType = FindType(
                "CryingSnow.StackCraft.NpcTradeService");
            Type traderType = FindType(
                "CryingSnow.StackCraft.NpcTrader");
            Type definitionType = FindType(
                "CryingSnow.StackCraft.CardDefinition");
            Type quoteType = FindType(
                "CryingSnow.StackCraft.MarketQuote");
            MethodInfo quoteMethod = serviceType.GetMethod(
                "TryGetMarketQuote",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    traderType,
                    definitionType,
                    quoteType.MakeByRefType()
                },
                null);
            Assert.That(quoteMethod, Is.Not.Null,
                "NPC 门面必须让 UI 与最终成交读取同一个 MarketQuote。");
            Assert.That(
                serviceType.GetMethod(
                    "TryPurchase",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[]
                    {
                        traderType,
                        FindType(
                            "CryingSnow.StackCraft.LocationMarketOffer"),
                        quoteType,
                        typeof(string).MakeByRefType()
                    },
                    null),
                Is.Not.Null,
                "购买必须携带 UI 展示的 MarketQuote。");
            Assert.That(
                serviceType.GetMethod(
                    "TrySellFromBackpack",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[]
                    {
                        traderType,
                        typeof(string),
                        typeof(int),
                        quoteType,
                        typeof(string).MakeByRefType()
                    },
                    null),
                Is.Not.Null,
                "出售确认必须携带 UI 展示的 MarketQuote。");

            ScriptableObject location =
                AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/StackCraft/Resources/Locations/Location_RiverbendMarket.asset");
            SerializedProperty profiles =
                new SerializedObject(location)
                    .FindProperty("npcTradeProfiles");
            Assert.That(profiles.arraySize, Is.GreaterThan(0));
            ScriptableObject profile = profiles
                .GetArrayElementAtIndex(0)
                .objectReferenceValue as ScriptableObject;
            Assert.That(profile, Is.Not.Null);
            ScriptableObject market = new SerializedObject(profile)
                .FindProperty("marketProfile")
                .objectReferenceValue as ScriptableObject;
            Assert.That(market, Is.Not.Null,
                "河湾杂货商必须绑定河湾地区公共市场。");
        }

        [Test]
        public void LegacyRiverbendMigration_IsAggregatedAndIdempotent()
        {
            Type migrationType = FindType(
                "CryingSnow.StackCraft.MarketEconomyMigration");
            Type mappingType = FindType(
                "CryingSnow.StackCraft.LegacyMarketCommodityMapping");
            Assert.That(migrationType, Is.Not.Null,
                "旧河湾市场存档迁移器尚未实现。");

            Type gameDataType = FindType(
                "CryingSnow.StackCraft.GameData");
            Type sceneDataType = FindType(
                "CryingSnow.StackCraft.SceneData");
            Type stateType = FindType(
                "CryingSnow.StackCraft.MarketStateData");
            Type commodityStateType = FindType(
                "CryingSnow.StackCraft.MarketCommodityStateData");
            Type stockType = FindType(
                "CryingSnow.StackCraft.MarketStockData");
            Type npcStateType = FindType(
                "CryingSnow.StackCraft.NpcTradeStateData");
            object gameData = Activator.CreateInstance(gameDataType);
            object sceneData = Activator.CreateInstance(sceneDataType);
            SetPublicField(
                sceneData,
                "SceneName",
                "Location/riverbend-market");
            object state = Activator.CreateInstance(stateType);
            SetPublicField(state, "MarketId", "riverbend-market");
            SetPublicField(state, "AvailableFunds", 300);
            object foodState =
                Activator.CreateInstance(commodityStateType);
            SetPublicField(foodState, "CommodityId", "food");
            SetPublicField(foodState, "Stock", 20);
            ((IList)GetPublicField(state, "Commodities"))
                .Add(foodState);

            IList legacyStock =
                (IList)GetPublicField(sceneData, "MarketStock");
            legacyStock.Add(Activator.CreateInstance(
                stockType,
                "apple-offer",
                1,
                2));
            legacyStock.Add(Activator.CreateInstance(
                stockType,
                "berry-offer",
                1,
                3));
            object npcState = Activator.CreateInstance(npcStateType);
            SetPublicProperty(npcState, "NpcId", "grocer");
            SetPublicProperty(npcState, "Day", 1);
            SetPublicProperty(npcState, "AvailableFunds", 77);
            ((IList)GetPublicField(sceneData, "NpcTrades"))
                .Add(npcState);

            Array mappings = CreateTypedArray(
                mappingType,
                Activator.CreateInstance(
                    mappingType,
                    "food",
                    "apple-offer"),
                Activator.CreateInstance(
                    mappingType,
                    "food",
                    "berry-offer"));
            MethodInfo migrate = migrationType.GetMethod(
                "MigrateRiverbendLegacy",
                BindingFlags.Public | BindingFlags.Static);
            object unresolved = migrate.Invoke(
                null,
                new[]
                {
                    gameData,
                    state,
                    null,
                    "grocer",
                    (object)1,
                    300,
                    mappings
                });
            Assert.That(unresolved, Is.False);
            Assert.That(
                GetPublicField(gameData, "EconomyStateVersion"),
                Is.EqualTo(0),
                "旧场景尚未解析时不能提前封版迁移。");
            migrate.Invoke(
                null,
                new[]
                {
                    gameData,
                    state,
                    sceneData,
                    "grocer",
                    (object)1,
                    300,
                    mappings
                });
            string first = MarketSnapshot(state);
            Assert.That(
                GetPublicField(foodState, "Stock"),
                Is.EqualTo(5));
            Assert.That(
                GetPublicField(state, "AvailableFunds"),
                Is.EqualTo(77));
            Assert.That(
                GetPublicField(gameData, "EconomyStateVersion"),
                Is.EqualTo(1));

            migrate.Invoke(
                null,
                new[]
                {
                    gameData,
                    state,
                    sceneData,
                    "grocer",
                    (object)1,
                    300,
                    mappings
                });
            Assert.That(MarketSnapshot(state), Is.EqualTo(first),
                "重复加载旧存档不能再次复制库存或资金。");
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(type => type != null);
        }

        private static int AssetQuote(
            ScriptableObject profile,
            string commodityId,
            int stock,
            string priceProperty)
        {
            SerializedObject serializedProfile =
                new SerializedObject(profile);
            SerializedProperty rules =
                serializedProfile.FindProperty("commodityRules");
            ScriptableObject commodity = null;
            object rule = null;
            for (int index = 0; index < rules.arraySize; index++)
            {
                SerializedProperty candidate =
                    rules.GetArrayElementAtIndex(index);
                ScriptableObject candidateCommodity =
                    candidate.FindPropertyRelative("commodity")
                        .objectReferenceValue as ScriptableObject;
                if (candidateCommodity == null)
                    continue;
                string id = new SerializedObject(candidateCommodity)
                    .FindProperty("id").stringValue;
                if (id != commodityId)
                    continue;
                commodity = candidateCommodity;
                rule = GetPrivateField(profile, "commodityRules")
                    is IList runtimeRules
                        ? runtimeRules[index]
                        : null;
                break;
            }
            Assert.That(rule, Is.Not.Null,
                $"{profile.name} 缺少商品 {commodityId}");

            object state = Activator.CreateInstance(
                FindType("CryingSnow.StackCraft.MarketStateData"));
            SetPublicField(
                state,
                "MarketId",
                serializedProfile.FindProperty("id").stringValue);
            SetPublicField(state, "AvailableFunds", 1000);
            object commodityState = Activator.CreateInstance(
                FindType(
                    "CryingSnow.StackCraft.MarketCommodityStateData"));
            SetPublicField(commodityState, "CommodityId", commodityId);
            SetPublicField(commodityState, "Stock", stock);
            ((IList)GetPublicField(state, "Commodities"))
                .Add(commodityState);
            object calculator = Activator.CreateInstance(
                FindType(
                    "CryingSnow.StackCraft.MarketQuoteCalculator"));
            object quote = calculator.GetType().GetMethod("Calculate")
                .Invoke(
                    calculator,
                    new[]
                    {
                        profile,
                        rule,
                        state,
                        Activator.CreateInstance(
                            FindType(
                                "CryingSnow.StackCraft.MerchantPriceModifiers")),
                        (object)0L,
                        1f
                    });
            Assert.That(commodity, Is.Not.Null);
            return GetIntProperty(quote, priceProperty);
        }

        private static void AssertRouteMarginAcrossStockRange(
            ScriptableObject sourceMarket,
            ScriptableObject destinationMarket,
            string commodityId)
        {
            foreach (int sourceStock in new[] { 18, 22 })
            {
                foreach (int destinationStock in new[] { 18, 22 })
                {
                    int buyPrice = AssetQuote(
                        sourceMarket,
                        commodityId,
                        sourceStock,
                        "PlayerBuyUnitPrice");
                    int sellPrice = AssetQuote(
                        destinationMarket,
                        commodityId,
                        destinationStock,
                        "PlayerSellUnitPrice");
                    double margin =
                        (sellPrice - buyPrice - 1d) / buyPrice;
                    Assert.That(
                        margin,
                        Is.InRange(.15d, .35d),
                        $"{commodityId} 路线在库存 {sourceStock}→" +
                        $"{destinationStock} 时净毛利失去 P0 目标区间。");
                }
            }
        }

        private static void AssertTenRefreshesStayValid(
            IEnumerable<ScriptableObject> markets)
        {
            Type stateType = FindType(
                "CryingSnow.StackCraft.MarketStateData");
            Type engineType = FindType(
                "CryingSnow.StackCraft.MarketRefreshEngine");
            object engine = Activator.CreateInstance(engineType);
            MethodInfo initialize = engineType.GetMethod("Initialize");
            MethodInfo catchUp = engineType.GetMethod("CatchUp");
            foreach (ScriptableObject market in markets)
            {
                object state = Activator.CreateInstance(stateType);
                initialize.Invoke(
                    engine,
                    new[] { market, state, (object)0L, 417 });
                for (int round = 0; round < 10; round++)
                {
                    long next = (long)GetPublicField(
                        state,
                        "NextRefreshWorldHour");
                    catchUp.Invoke(
                        engine,
                        new[] { market, state, (object)next, 417 });
                    Assert.That(
                        (int)GetPublicField(state, "AvailableFunds"),
                        Is.GreaterThanOrEqualTo(0));
                    foreach (object commodityState in
                             (IList)GetPublicField(
                                 state,
                                 "Commodities"))
                    {
                        Assert.That(
                            (int)GetPublicField(
                                commodityState,
                                "Stock"),
                            Is.InRange(0, 60));
                    }
                }
            }
        }

        private static object CreateMarketSetup(
            string marketId,
            string commodityId,
            int basePrice,
            float regionalFactor,
            int initialMin,
            int initialMax)
        {
            Type commodityType = FindType(
                "CryingSnow.StackCraft.CommodityDefinition");
            Type profileType = FindType(
                "CryingSnow.StackCraft.MarketProfile");
            Type ruleType = FindType(
                "CryingSnow.StackCraft.MarketCommodityRule");
            ScriptableObject commodity =
                ScriptableObject.CreateInstance(commodityType);
            ScriptableObject profile =
                ScriptableObject.CreateInstance(profileType);
            SetField(commodity, "id", commodityId);
            SetField(commodity, "basePrice", basePrice);
            SetField(commodity, "volatility", 0.35f);
            SetField(profile, "id", marketId);
            SetField(profile, "locationId", marketId);
            SetField(profile, "startingFunds", 200);
            SetField(profile, "maximumFunds", 400);
            SetField(profile, "fundRecovery", 20);
            SetField(profile, "refreshHoursMin", 48);
            SetField(profile, "refreshHoursMax", 72);
            SetField(profile, "baseSpread", 0.18f);
            object rule = Activator.CreateInstance(ruleType);
            SetField(rule, "commodity", commodity);
            SetField(rule, "regionalPriceFactor", regionalFactor);
            SetField(rule, "initialStockMin", initialMin);
            SetField(rule, "initialStockMax", initialMax);
            SetField(rule, "targetStock", 10);
            SetField(rule, "productionPerRefresh", 2);
            SetField(rule, "consumptionPerRefresh", 1);
            SetField(rule, "minimumStock", 0);
            SetField(rule, "maximumStock", 20);
            SetField(rule, "allowsPlayerPurchase", true);
            SetField(rule, "allowsPlayerSale", true);
            ((IList)GetPrivateField(profile, "commodityRules")).Add(rule);
            return new MarketSetup(profile, commodity);
        }

        private static object GetTupleValue(object setup, string name)
        {
            return setup.GetType().GetProperty(name).GetValue(setup);
        }

        private static object GetPrivateField(object target, string name)
        {
            return target.GetType().GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        private static string Snapshot(object state)
        {
            object commodity =
                ((IList)GetPublicField(state, "Commodities"))[0];
            return string.Join(
                "|",
                GetPublicField(state, "AvailableFunds"),
                GetPublicField(state, "NextRefreshWorldHour"),
                GetPublicField(state, "RefreshSequence"),
                GetPublicField(state, "StateRevision"),
                GetPublicField(commodity, "Stock"));
        }

        private static string MarketSnapshot(object state)
        {
            object commodity =
                ((IList)GetPublicField(state, "Commodities"))[0];
            return string.Join(
                "|",
                GetPublicField(state, "AvailableFunds"),
                GetPublicField(state, "StateRevision"),
                GetPublicField(commodity, "Stock"));
        }

        private static string InventorySnapshot(object inventory)
        {
            return string.Join(
                "|",
                GetIntProperty(inventory, "Currency"),
                inventory.GetType().GetMethod("CountCommodity").Invoke(
                    inventory,
                    new object[] { "food" }));
        }

        private static Array CreateTypedArray(
            Type elementType,
            params object[] values)
        {
            Array array = Array.CreateInstance(elementType, values.Length);
            for (int index = 0; index < values.Length; index++)
                array.SetValue(values[index], index);
            return array;
        }

        private static bool GetBoolProperty(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, $"缺少属性 {name}");
            return (bool)property.GetValue(target);
        }

        private static void SetField(
            object target,
            string name,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"缺少字段 {name}");
            field.SetValue(target, value);
        }

        private static void SetPublicField(
            object target,
            string name,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"缺少字段 {name}");
            field.SetValue(target, value);
        }

        private static void SetPublicProperty(
            object target,
            string name,
            object value)
        {
            PropertyInfo property = target.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, $"缺少属性 {name}");
            property.SetValue(target, value);
        }

        private static object GetPublicField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"缺少字段 {name}");
            return field.GetValue(target);
        }

        private static int GetIntProperty(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, $"缺少属性 {name}");
            return (int)property.GetValue(target);
        }

        private sealed class MarketSetup
        {
            public MarketSetup(
                ScriptableObject profile,
                ScriptableObject commodity)
            {
                Profile = profile;
                Commodity = commodity;
            }

            public ScriptableObject Profile { get; }
            public ScriptableObject Commodity { get; }
        }
    }
}
