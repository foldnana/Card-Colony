using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public static class NpcTradeService
    {
        public static void EnsureState(NpcTrader trader)
        {
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
                    !trader.CanBuy(card.Definition) ||
                    (card.Combatant != null && card.Combatant.IsInCombat)))
            {
                return Reject("该组物品当前无法出售。", out reason);
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
            if (trader.Card.Stack == null || trader.Card.Stack.IsLocked)
                return Reject("这个人物正忙，暂时不能交易。", out reason);

            reason = string.Empty;
            return true;
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
