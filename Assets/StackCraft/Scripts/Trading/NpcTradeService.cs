using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public static class NpcTradeService
    {
        public static int ApplyRobberyLoss(
            NpcTrader trader,
            float lossFraction)
        {
            float fraction = Mathf.Clamp01(lossFraction);
            if (trader?.Profile == null || fraction <= 0f)
                return 0;

            int stolen = 0;
            if (TryGetRegionalMarket(trader, out ResourcesMarketCatalog catalog,
                    out MarketService service))
            {
                MarketStateData state = service.GetOrCreateState(
                    trader.Profile.MarketProfile.Id);
                MigrateLegacyRiverbendState(trader, state);
                var merchantCommodityIds = new HashSet<string>(
                    trader.SellOffers
                        .Select(offer => catalog.FindCommodity(
                            offer.ProductDefinition)?.Id)
                        .Where(id => !string.IsNullOrWhiteSpace(id)),
                    StringComparer.Ordinal);
                if (merchantCommodityIds.Count == 0)
                    return 0;
                foreach (MarketCommodityStateData commodity in
                         state?.Commodities ??
                         Enumerable.Empty<MarketCommodityStateData>())
                {
                    if (commodity == null || commodity.Stock <= 0)
                        continue;
                    if (!merchantCommodityIds.Contains(commodity.CommodityId))
                    {
                        continue;
                    }
                    int removed = Mathf.Clamp(
                        Mathf.CeilToInt(commodity.Stock * fraction),
                        1,
                        commodity.Stock);
                    commodity.Stock -= removed;
                    stolen += removed;
                }
                if (stolen > 0)
                    state.StateRevision++;
                CardManager.Instance?.NotifyStatsChanged();
                return stolen;
            }

            if (!TryGetContext(trader, out SceneData sceneData, out int day))
                return 0;
            foreach (LocationMarketOffer offer in trader.SellOffers)
            {
                MarketStockData stock = MarketStockLedger.GetOrRefreshNpc(
                    sceneData,
                    GetNpcId(trader),
                    offer.StockId,
                    day,
                    offer.MinimumDailyStock,
                    offer.MaximumDailyStock);
                int removed = Mathf.Clamp(
                    Mathf.CeilToInt(stock.Remaining * fraction),
                    0,
                    stock.Remaining);
                stock.Remaining -= removed;
                stolen += removed;
            }
            CardManager.Instance?.NotifyStatsChanged();
            return stolen;
        }

        public static void EnsureState(NpcTrader trader)
        {
            if (TryGetRegionalMarket(
                    trader,
                    out _,
                    out MarketService marketService))
            {
                MarketStateData state = marketService.GetOrCreateState(
                    trader.Profile.MarketProfile.Id);
                MigrateLegacyRiverbendState(trader, state);
                return;
            }

            if (!TryGetContext(
                    trader,
                    out SceneData sceneData,
                    out int day))
            {
                return;
            }

            NpcTradeLedger.GetOrRefresh(
                sceneData,
                GetNpcId(trader),
                day,
                trader.Profile.StartingFunds);
        }

        public static int GetAvailableFunds(NpcTrader trader)
        {
            if (TryGetRegionalMarket(
                    trader,
                    out _,
                    out MarketService marketService))
            {
                MarketStateData state = marketService.GetOrCreateState(
                    trader.Profile.MarketProfile.Id);
                MigrateLegacyRiverbendState(trader, state);
                return state?.AvailableFunds ?? 0;
            }

            if (!TryGetContext(
                    trader,
                    out SceneData sceneData,
                    out int day))
            {
                return trader?.Profile?.StartingFunds ?? 0;
            }

            return NpcTradeLedger.GetOrRefresh(
                sceneData,
                GetNpcId(trader),
                day,
                trader.Profile.StartingFunds).AvailableFunds;
        }

        public static int GetStock(
            NpcTrader trader,
            LocationMarketOffer offer)
        {
            if (TryGetMarketQuote(
                    trader,
                    offer.ProductDefinition,
                    out MarketQuote quote))
            {
                return quote.AvailableStock;
            }

            if (trader == null ||
                !TryGetContext(trader, out SceneData sceneData, out int day))
            {
                return 0;
            }

            return MarketStockLedger.GetOrRefreshNpc(
                sceneData,
                GetNpcId(trader),
                offer.StockId,
                day,
                offer.MinimumDailyStock,
                offer.MaximumDailyStock).Remaining;
        }

        public static IReadOnlyList<NpcTradeStockData> GetAcquiredStock(
            NpcTrader trader)
        {
            if (trader == null ||
                !TryGetContext(trader, out SceneData sceneData, out int day))
            {
                return Array.Empty<NpcTradeStockData>();
            }

            return NpcTradeLedger.GetAllAcquiredStock(
                sceneData,
                GetNpcId(trader),
                day,
                trader.Profile.StartingFunds);
        }

        public static bool TryPurchase(
            NpcTrader trader,
            LocationMarketOffer offer,
            out string reason)
        {
            reason = string.Empty;
            if (trader?.Card == null || offer.ProductDefinition == null)
                return Reject("商品配置无效。", out reason);
            if (!CanTradeNow(trader, out reason))
                return false;
            if (trader.Profile?.MarketProfile != null)
            {
                if (!TryGetMarketQuote(
                        trader,
                        offer.ProductDefinition,
                        out MarketQuote expectedQuote))
                {
                    return Reject(
                        "该商品尚未加入地区市场。",
                        out reason);
                }
                return TryPurchaseFromRegionalMarket(
                    trader,
                    offer,
                    expectedQuote,
                    out reason);
            }
            if (trader.Currency == null || CardManager.Instance == null)
                return Reject("交易服务尚未就绪。", out reason);
            if (!TryGetContext(trader, out SceneData sceneData, out int day))
                return Reject("无法读取当前地点交易状态。", out reason);
            if (GetStock(trader, offer) <= 0)
                return Reject("今天已经售罄。", out reason);
            int purchasePrice = trader.GetPlayerBuyPrice(offer);
            if (MarketCurrencyService.CountAvailable(
                    trader.Currency,
                    BackpackService.Current,
                    CardManager.Instance.AllCards) < purchasePrice)
            {
                return Reject("金币不足。", out reason);
            }

            BackpackData backpack = BackpackService.Current;
            if (backpack == null ||
                !BackpackService.CanStoreDefinition(offer.ProductDefinition))
            {
                return Reject("背包无法接收这件商品。", out reason);
            }

            var previousEntryIds = new HashSet<string>(
                backpack.Entries
                    .Where(entry => entry != null)
                    .Select(entry => entry.InstanceId));
            if (!TryDeliverPurchasedItem(
                    offer.ProductDefinition,
                    backpack))
            {
                return Reject("商品放入背包失败。", out reason);
            }

            BackpackEntryData deliveredEntry = backpack.Entries
                .FirstOrDefault(entry =>
                    entry != null &&
                    !previousEntryIds.Contains(entry.InstanceId));
            if (deliveredEntry == null)
                return Reject("商品放入背包失败。", out reason);

            if (!MarketStockLedger.TryConsumeNpc(
                    sceneData,
                    GetNpcId(trader),
                    offer.StockId,
                    day,
                    offer.MinimumDailyStock,
                    offer.MaximumDailyStock))
            {
                backpack.TryRemove(deliveredEntry.InstanceId, out _);
                return Reject("库存刚刚发生变化，请重试。", out reason);
            }

            if (!MarketCurrencyService.TrySpend(
                    trader.Currency,
                    purchasePrice,
                    backpack,
                    CardManager.Instance.AllCards))
            {
                MarketStockLedger.RestoreNpc(
                    sceneData,
                    GetNpcId(trader),
                    offer.StockId,
                    day,
                    offer.MinimumDailyStock,
                    offer.MaximumDailyStock);
                backpack.TryRemove(deliveredEntry.InstanceId, out _);
                return Reject("金币扣除失败。", out reason);
            }

            NpcTradeLedger.AddFunds(
                sceneData,
                GetNpcId(trader),
                day,
                trader.Profile.StartingFunds,
                purchasePrice);
            CardManager.Instance.NotifyStatsChanged();
            AudioManager.Instance?.PlaySFX(AudioId.CashRegister);
            return true;
        }

        public static bool TryPurchase(
            NpcTrader trader,
            LocationMarketOffer offer,
            MarketQuote expectedQuote,
            out string reason)
        {
            reason = string.Empty;
            if (trader?.Card == null || offer.ProductDefinition == null)
                return Reject("商品配置无效。", out reason);
            if (!CanTradeNow(trader, out reason))
                return false;
            if (trader.Profile?.MarketProfile == null)
                return TryPurchase(trader, offer, out reason);
            return TryPurchaseFromRegionalMarket(
                trader,
                offer,
                expectedQuote,
                out reason);
        }

        public static bool TryDeliverPurchasedItem(
            CardDefinition definition,
            BackpackData backpack)
        {
            return BackpackService.TryStoreGeneratedCardsDeferred(
                definition,
                1,
                backpack);
        }

        public static bool TryPurchaseAcquired(
            NpcTrader trader,
            string productId,
            out string reason)
        {
            reason = string.Empty;
            if (trader?.Profile == null ||
                trader.Currency == null ||
                CardManager.Instance == null)
            {
                return Reject("交易服务尚未就绪。", out reason);
            }
            if (!CanTradeNow(trader, out reason))
                return false;
            if (string.IsNullOrWhiteSpace(productId))
                return Reject("商品配置无效。", out reason);
            if (!TryGetContext(trader, out SceneData sceneData, out int day))
                return Reject("无法读取当前地点交易状态。", out reason);

            CardDefinition definition =
                CardManager.Instance.GetDefinitionById(productId);
            if (definition == null ||
                NpcTradeLedger.GetAcquiredStock(
                    sceneData,
                    GetNpcId(trader),
                    day,
                    trader.Profile.StartingFunds,
                    productId) <= 0)
            {
                return Reject("这件个人物品已经售罄。", out reason);
            }

            int purchasePrice = trader.GetAcquiredBuybackPrice(definition);
            BackpackData backpack = BackpackService.Current;
            if (backpack == null ||
                !BackpackService.CanStoreDefinition(definition))
            {
                return Reject("背包无法接收这件商品。", out reason);
            }
            if (MarketCurrencyService.CountAvailable(
                    trader.Currency,
                    backpack,
                    CardManager.Instance.AllCards) < purchasePrice)
            {
                return Reject("金币不足。", out reason);
            }

            var previousEntryIds = new HashSet<string>(
                backpack.Entries
                    .Where(entry => entry != null)
                    .Select(entry => entry.InstanceId));
            if (!TryDeliverPurchasedItem(definition, backpack))
                return Reject("商品放入背包失败。", out reason);

            BackpackEntryData deliveredEntry = backpack.Entries
                .FirstOrDefault(entry =>
                    entry != null &&
                    !previousEntryIds.Contains(entry.InstanceId));
            if (deliveredEntry == null)
                return Reject("商品放入背包失败。", out reason);

            if (!NpcTradeLedger.TryConsumeAcquiredStock(
                    sceneData,
                    GetNpcId(trader),
                    day,
                    trader.Profile.StartingFunds,
                    productId))
            {
                backpack.TryRemove(deliveredEntry.InstanceId, out _);
                return Reject("个人库存刚刚发生变化，请重试。", out reason);
            }

            if (!MarketCurrencyService.TrySpend(
                    trader.Currency,
                    purchasePrice,
                    backpack,
                    CardManager.Instance.AllCards))
            {
                NpcTradeLedger.RestoreAcquiredStock(
                    sceneData,
                    GetNpcId(trader),
                    day,
                    trader.Profile.StartingFunds,
                    productId);
                backpack.TryRemove(deliveredEntry.InstanceId, out _);
                return Reject("金币扣除失败。", out reason);
            }

            NpcTradeLedger.AddFunds(
                sceneData,
                GetNpcId(trader),
                day,
                trader.Profile.StartingFunds,
                purchasePrice);
            CardManager.Instance.NotifyStatsChanged();
            AudioManager.Instance?.PlaySFX(AudioId.CashRegister);
            return true;
        }

        public static bool TrySellFromBackpack(
            NpcTrader trader,
            string productId,
            int requestedCount,
            out string reason)
        {
            reason = string.Empty;
            BackpackData backpack = BackpackService.Current;
            if (trader?.Profile == null || backpack == null)
                return Reject("交易服务尚未就绪。", out reason);
            if (!CanTradeNow(trader, out reason))
                return false;
            if (string.IsNullOrWhiteSpace(productId) || requestedCount <= 0)
                return Reject("请选择要出售的物品。", out reason);

            CardDefinition definition =
                CardManager.Instance?.GetDefinitionById(productId);
            if (!trader.CanBuy(definition))
                return Reject(
                    trader.Profile.RefusalText,
                    out reason);
            if (trader.Profile.MarketProfile != null)
            {
                if (!TryGetMarketQuote(
                        trader,
                        definition,
                        out MarketQuote expectedQuote))
                {
                    return Reject(
                        trader.Profile.RefusalText,
                        out reason);
                }
                return TrySellToRegionalMarket(
                    trader,
                    definition,
                    requestedCount,
                    expectedQuote,
                    out reason);
            }

            List<BackpackEntryData> entries = backpack.Entries
                .Where(entry => entry?.Card?.Id == productId)
                .Take(requestedCount)
                .ToList();
            if (entries.Count < requestedCount)
                return Reject("持有数量不足。", out reason);

            int proceeds =
                trader.GetPlayerSellPrice(definition) * entries.Count;
            if (!TryGetContext(trader, out SceneData sceneData, out int day))
                return Reject("无法读取当前地点交易状态。", out reason);
            if (GetAvailableFunds(trader) < proceeds)
                return Reject("这个人物没有足够的钱收购。", out reason);

            bool transferred = BackpackService.TryTakeExistingStack(
                backpack,
                entries.Select(entry => entry.InstanceId).ToList(),
                () =>
                {
                    if (!NpcTradeLedger.TrySpendFunds(
                            sceneData,
                            GetNpcId(trader),
                            day,
                            trader.Profile.StartingFunds,
                            proceeds))
                    {
                        return false;
                    }

                    if (!BackpackService.TryStoreGeneratedCardsDeferred(
                            trader.Currency,
                            proceeds,
                            backpack))
                    {
                        NpcTradeLedger.AddFunds(
                            sceneData,
                            GetNpcId(trader),
                            day,
                            trader.Profile.StartingFunds,
                            proceeds);
                        return false;
                    }

                    NpcTradeLedger.AddAcquiredStock(
                        sceneData,
                        GetNpcId(trader),
                        day,
                        trader.Profile.StartingFunds,
                        productId,
                        entries.Count);
                    return true;
                });
            if (!transferred)
                return Reject("交易未完成，物品已保留。", out reason);

            AudioManager.Instance?.PlaySFX(AudioId.Coins);
            return true;
        }

        public static bool TrySellFromBackpack(
            NpcTrader trader,
            string productId,
            int requestedCount,
            MarketQuote expectedQuote,
            out string reason)
        {
            reason = string.Empty;
            BackpackData backpack = BackpackService.Current;
            if (trader?.Profile == null || backpack == null)
                return Reject("交易服务尚未就绪。", out reason);
            if (!CanTradeNow(trader, out reason))
                return false;
            if (string.IsNullOrWhiteSpace(productId) ||
                requestedCount <= 0)
            {
                return Reject("请选择要出售的物品。", out reason);
            }

            CardDefinition definition =
                CardManager.Instance?.GetDefinitionById(productId);
            if (!trader.CanBuy(definition))
                return Reject(trader.Profile.RefusalText, out reason);
            if (trader.Profile.MarketProfile == null)
            {
                return TrySellFromBackpack(
                    trader,
                    productId,
                    requestedCount,
                    out reason);
            }
            return TrySellToRegionalMarket(
                trader,
                definition,
                requestedCount,
                expectedQuote,
                out reason);
        }

        public static bool TrySellWorldStack(
            NpcTrader trader,
            CardStack stack,
            out string reason)
        {
            reason = string.Empty;
            if (trader?.Profile == null || stack?.Cards == null ||
                stack.Cards.Count == 0 || trader.Currency == null)
            {
                return Reject("没有待确认的桌面物品。", out reason);
            }
            if (!CanTradeNow(trader, out reason))
                return false;

            List<CardInstance> cards = stack.Cards.ToList();
            if (cards.Any(card => card == null ||
                    !ProtagonistRules.CanBeSold(card) ||
                    !trader.CanBuy(card.Definition) ||
                    (card.Combatant != null && card.Combatant.IsInCombat)))
            {
                return Reject("该组物品当前无法出售。", out reason);
            }
            if (trader.Profile.MarketProfile != null)
            {
                return TrySellWorldStackToRegionalMarket(
                    trader,
                    stack,
                    cards,
                    out reason);
            }

            int proceeds = cards.Sum(card =>
                trader.GetPlayerSellPrice(card.Definition));
            if (!TryGetContext(trader, out SceneData sceneData, out int day))
                return Reject("无法读取当前地点交易状态。", out reason);
            if (GetAvailableFunds(trader) < proceeds)
                return Reject("这个人物没有足够的钱收购。", out reason);
            if (!NpcTradeLedger.TrySpendFunds(
                    sceneData,
                    GetNpcId(trader),
                    day,
                    trader.Profile.StartingFunds,
                    proceeds))
            {
                return Reject("这个人物的资金刚刚发生变化。", out reason);
            }

            if (!BackpackService.TryStoreGeneratedCardsDeferred(
                    trader.Currency,
                    proceeds,
                    BackpackService.Current))
            {
                NpcTradeLedger.AddFunds(
                    sceneData,
                    GetNpcId(trader),
                    day,
                    trader.Profile.StartingFunds,
                    proceeds);
                return Reject("背包无法接收金币。", out reason);
            }

            foreach (IGrouping<string, CardInstance> group in
                     cards.GroupBy(card => card.Definition.Id))
            {
                NpcTradeLedger.AddAcquiredStock(
                    sceneData,
                    GetNpcId(trader),
                    day,
                    trader.Profile.StartingFunds,
                    group.Key,
                    group.Count());
            }

            TradeManager.Instance?.NotifyCardsSold(stack);
            stack.DestroyAllCards();
            BackpackService.NotifyContentsChanged();
            AudioManager.Instance?.PlaySFX(AudioId.Coins);
            return true;
        }

        private static bool TryGetContext(
            NpcTrader trader,
            out SceneData sceneData,
            out int day)
        {
            sceneData = null;
            day = TimeManager.Instance?.CurrentDay ?? 1;
            if (trader?.Profile == null ||
                GameDirector.Instance?.GameData == null)
            {
                return false;
            }

            day = GameDirector.Instance.GameData.GetWorldDay(day);
            GameDirector.Instance.GameData.TryGetScene(out sceneData);
            return sceneData != null;
        }

        public static bool CanTradeNow(
            NpcTrader trader,
            out string reason)
        {
            if (trader?.Card == null || trader.Profile == null)
                return Reject("这个人物当前不能交易。", out reason);
            if (trader.Card.Combatant != null &&
                trader.Card.Combatant.IsInCombat)
            {
                return Reject("战斗中的人物不能交易。", out reason);
            }
            if (DialogueManager.Instance != null &&
                DialogueManager.Instance.IsCardInDialogue(trader.Card))
            {
                return Reject("请先结束当前交谈。", out reason);
            }
            NpcInteractionManager interaction =
                NpcInteractionManager.Instance;
            bool isInteractionTarget =
                interaction?.IsActive == true &&
                interaction.IsPlayerInvolved &&
                interaction.Npc == trader.Card &&
                (interaction.State ==
                    NpcInteractionState.ChoosingAction ||
                 interaction.State ==
                    NpcInteractionState.Trade);
            if (!isInteractionTarget)
            {
                return Reject(
                    "请先让玩家人物与这个人物开始互动。",
                    out reason);
            }
            if (trader.Card.Stack?.IsLocked == true)
                return Reject("这个人物正忙，暂时不能交易。", out reason);

            reason = string.Empty;
            return true;
        }

        public static bool TryGetMarketQuote(
            NpcTrader trader,
            CardDefinition definition,
            out MarketQuote quote)
        {
            quote = default;
            if (!TryGetRegionalMarket(
                    trader,
                    out ResourcesMarketCatalog catalog,
                    out MarketService service))
            {
                return false;
            }

            CommodityDefinition commodity =
                catalog.FindCommodity(definition);
            if (commodity == null)
                return false;

            RegisterMerchantPolicy(trader, catalog, service);
            quote = service.GetQuote(
                trader.Profile.MarketProfile.Id,
                commodity.Id,
                GetModifiers(trader));
            return !string.IsNullOrWhiteSpace(quote.CommodityId);
        }

        private static bool TryPurchaseFromRegionalMarket(
            NpcTrader trader,
            LocationMarketOffer offer,
            MarketQuote expectedQuote,
            out string reason)
        {
            reason = string.Empty;
            if (!TryGetRegionalMarket(
                    trader,
                    out ResourcesMarketCatalog catalog,
                    out MarketService service))
            {
                return Reject("地区市场服务尚未就绪。", out reason);
            }

            CommodityDefinition commodity =
                catalog.FindCommodity(offer.ProductDefinition);
            if (commodity == null)
            {
                return Reject(
                    "该商品尚未加入地区市场。",
                    out reason);
            }
            if (expectedQuote.MarketId !=
                    trader.Profile.MarketProfile.Id ||
                expectedQuote.CommodityId != commodity.Id)
            {
                return Reject("报价与所选商品不匹配。", out reason);
            }
            RegisterMerchantPolicy(trader, catalog, service);
            var request = new MarketTradeRequest(
                expectedQuote.MarketId,
                trader.TradeStateId,
                commodity.Id,
                MarketTradeDirection.PlayerBuys,
                1,
                expectedQuote.PlayerBuyUnitPrice,
                expectedQuote.StateRevision,
                GetModifiers(trader),
                GetFilter(trader, catalog));
            var inventory = new CardBackpackTradeInventory(
                catalog,
                trader.Currency,
                commodity.Id,
                offer.ProductDefinition);
            MarketTradeResult result = service.Execute(
                request,
                inventory);
            if (!result.Success)
                return Reject(DescribeFailure(result.Failure), out reason);

            CardManager.Instance?.NotifyStatsChanged();
            AudioManager.Instance?.PlaySFX(AudioId.CashRegister);
            return true;
        }

        private static bool TrySellToRegionalMarket(
            NpcTrader trader,
            CardDefinition definition,
            int quantity,
            MarketQuote expectedQuote,
            out string reason)
        {
            reason = string.Empty;
            if (!TryGetRegionalMarket(
                    trader,
                    out ResourcesMarketCatalog catalog,
                    out MarketService service))
            {
                return Reject("地区市场服务尚未就绪。", out reason);
            }

            CommodityDefinition commodity =
                catalog.FindCommodity(definition);
            if (commodity == null)
                return Reject(trader.Profile.RefusalText, out reason);
            if (expectedQuote.MarketId !=
                    trader.Profile.MarketProfile.Id ||
                expectedQuote.CommodityId != commodity.Id)
            {
                return Reject("报价与所选商品不匹配。", out reason);
            }
            RegisterMerchantPolicy(trader, catalog, service);
            var request = new MarketTradeRequest(
                expectedQuote.MarketId,
                trader.TradeStateId,
                commodity.Id,
                MarketTradeDirection.PlayerSells,
                quantity,
                expectedQuote.PlayerSellUnitPrice,
                expectedQuote.StateRevision,
                GetModifiers(trader),
                GetFilter(trader, catalog));
            var inventory = new CardBackpackTradeInventory(
                catalog,
                trader.Currency,
                commodity.Id,
                definition);
            MarketTradeResult result = service.Execute(
                request,
                inventory);
            if (!result.Success)
                return Reject(DescribeFailure(result.Failure), out reason);

            if (TryGetContext(trader, out SceneData sceneData, out int day))
            {
                NpcTradeLedger.AddAcquiredStock(
                    sceneData,
                    GetNpcId(trader),
                    day,
                    trader.Profile.StartingFunds,
                    definition.Id,
                    quantity);
            }
            AudioManager.Instance?.PlaySFX(AudioId.Coins);
            return true;
        }

        private static bool TrySellWorldStackToRegionalMarket(
            NpcTrader trader,
            CardStack stack,
            IReadOnlyList<CardInstance> cards,
            out string reason)
        {
            reason = string.Empty;
            if (!TryGetRegionalMarket(
                    trader,
                    out ResourcesMarketCatalog catalog,
                    out MarketService service))
            {
                return Reject("地区市场服务尚未就绪。", out reason);
            }

            CommodityDefinition commodity =
                catalog.FindCommodity(cards[0].Definition);
            if (commodity == null ||
                cards.Any(card =>
                    catalog.FindCommodity(card.Definition)?.Id !=
                    commodity.Id))
            {
                return Reject(
                    "地区市场一次只能收购同类商品。",
                    out reason);
            }

            MarketQuote quote = service.GetQuote(
                trader.Profile.MarketProfile.Id,
                commodity.Id,
                GetModifiers(trader));
            RegisterMerchantPolicy(trader, catalog, service);
            var request = new MarketTradeRequest(
                quote.MarketId,
                trader.TradeStateId,
                commodity.Id,
                MarketTradeDirection.PlayerSells,
                cards.Count,
                quote.PlayerSellUnitPrice,
                quote.StateRevision,
                GetModifiers(trader),
                GetFilter(trader, catalog));
            var inventory = new WorldStackTradeInventory(
                trader.Currency,
                commodity.Id,
                stack);
            MarketTradeResult result = service.Execute(
                request,
                inventory);
            if (!result.Success)
                return Reject(DescribeFailure(result.Failure), out reason);

            if (TryGetContext(trader, out SceneData sceneData, out int day))
            {
                foreach (IGrouping<string, CardInstance> group in
                         cards.GroupBy(card => card.Definition.Id))
                {
                    NpcTradeLedger.AddAcquiredStock(
                        sceneData,
                        GetNpcId(trader),
                        day,
                        trader.Profile.StartingFunds,
                        group.Key,
                        group.Count());
                }
            }
            AudioManager.Instance?.PlaySFX(AudioId.Coins);
            return true;
        }

        private static bool TryGetRegionalMarket(
            NpcTrader trader,
            out ResourcesMarketCatalog catalog,
            out MarketService service)
        {
            catalog = null;
            service = null;
            return trader?.Profile?.MarketProfile != null &&
                MarketRuntime.TryGet(out catalog, out service);
        }

        private static MerchantPriceModifiers GetModifiers(
            NpcTrader trader)
        {
            return new MerchantPriceModifiers(
                trader?.Profile?.SellPriceModifier ?? 1f,
                trader?.Profile?.BuyPriceModifier ?? 1f);
        }

        private static MerchantTradeFilter GetFilter(
            NpcTrader trader,
            ResourcesMarketCatalog catalog)
        {
            return new MerchantTradeFilter(
                ResolveCommodityIds(
                    trader?.Profile?.MarketProfile,
                    trader?.Profile?.SellCommodityTags),
                ResolveCommodityIds(
                    trader?.Profile?.MarketProfile,
                    trader?.Profile?.BuyCommodityTags));
        }

        private static IEnumerable<string> ResolveCommodityIds(
            MarketProfile market,
            IReadOnlyCollection<string> tags)
        {
            if (market == null || tags == null || tags.Count == 0)
                return null;

            return market.CommodityRules
                .Where(rule => rule?.Commodity != null &&
                    tags.Any(tag =>
                        tag == rule.Commodity.Id ||
                        rule.Commodity.HasTag(tag)))
                .Select(rule => rule.Commodity.Id)
                .Distinct()
                .ToArray();
        }

        private static void RegisterMerchantPolicy(
            NpcTrader trader,
            ResourcesMarketCatalog catalog,
            MarketService service)
        {
            if (trader == null || service == null)
                return;

            service.RegisterMerchantPolicy(
                trader.TradeStateId,
                GetFilter(trader, catalog),
                GetModifiers(trader));
        }

        private static string DescribeFailure(
            MarketTradeFailure failure)
        {
            return failure switch
            {
                MarketTradeFailure.StaleQuote =>
                    "行情刚刚变化，请重试。",
                MarketTradeFailure.InsufficientMarketStock =>
                    "市场库存不足。",
                MarketTradeFailure.InsufficientMarketFunds =>
                    "市场没有足够资金收购。",
                MarketTradeFailure.InsufficientPlayerCurrency =>
                    "金币不足。",
                MarketTradeFailure.InsufficientPlayerGoods =>
                    "持有数量不足。",
                MarketTradeFailure.InventoryCannotReceive =>
                    "背包无法接收这件商品。",
                MarketTradeFailure.MarketFundsCapacityReached =>
                    "市场资金已达上限，暂时停止销售。",
                MarketTradeFailure.MarketStockCapacityReached =>
                    "市场仓库已满，暂时停止收购。",
                MarketTradeFailure.MerchantDoesNotTradeCommodity =>
                    "这个人物不经营该商品。",
                MarketTradeFailure.DirectionClosed =>
                    "当前市场未开放这项交易。",
                _ => "交易未完成，资产没有发生变化。"
            };
        }

        private static void MigrateLegacyRiverbendState(
            NpcTrader trader,
            MarketStateData state)
        {
            GameData gameData = GameDirector.Instance?.GameData;
            if (gameData == null || state == null)
            {
                return;
            }

            if (!TryGetContext(
                    trader,
                    out SceneData sceneData,
                    out int day))
            {
                return;
            }
            ResourcesMarketCatalog catalog =
                MarketRuntime.TryGet(out var runtimeCatalog, out _)
                    ? runtimeCatalog
                    : null;
            var mappings =
                new List<LegacyMarketCommodityMapping>();
            foreach (LocationMarketOffer offer in trader.SellOffers)
            {
                CommodityDefinition commodity =
                    catalog?.FindCommodity(offer.ProductDefinition);
                if (commodity != null)
                    mappings.Add(new LegacyMarketCommodityMapping(
                        commodity.Id,
                        offer.StockId));
            }

            MarketEconomyMigration.MigrateRiverbendLegacy(
                gameData,
                state,
                sceneData,
                GetNpcId(trader),
                day,
                trader.Profile.StartingFunds,
                mappings);
        }

        private static string GetNpcId(NpcTrader trader)
        {
            return trader?.TradeStateId ?? trader?.gameObject.name;
        }

        private static bool Reject(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
