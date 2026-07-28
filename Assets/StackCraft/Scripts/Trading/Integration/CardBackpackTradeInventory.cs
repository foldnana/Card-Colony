using System;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public sealed class CardBackpackTradeInventory :
        IAtomicPlayerTradeInventory
    {
        private readonly ResourcesMarketCatalog catalog;
        private readonly CardDefinition currency;
        private readonly string overrideCommodityId;
        private readonly CardDefinition overrideDefinition;
        private List<string> pendingAddedEntryIds;
        private List<BackpackEntryData> pendingRemovedEntries;

        public CardBackpackTradeInventory(
            ResourcesMarketCatalog catalog,
            CardDefinition currency,
            string overrideCommodityId = null,
            CardDefinition overrideDefinition = null)
        {
            this.catalog = catalog;
            this.currency = currency;
            this.overrideCommodityId = overrideCommodityId;
            this.overrideDefinition = overrideDefinition;
        }

        public int CountCurrency(string currencyCardId)
        {
            return MarketCurrencyService.CountAvailable(
                currency,
                BackpackService.Current,
                CardManager.Instance?.AllCards);
        }

        public int CountCommodity(string commodityId)
        {
            CardDefinition definition =
                GetDefinition(commodityId);
            if (definition == null)
                return 0;
            return BackpackService.Current?.Entries?.Count(entry =>
                entry?.Card?.Id == definition.Id) ?? 0;
        }

        public bool CanReceive(string commodityId, int quantity)
        {
            CommodityDefinition commodity =
                catalog?.GetCommodity(commodityId);
            return quantity > 0 &&
                GetDefinition(commodityId) != null &&
                BackpackService.CanStoreDefinition(
                    GetDefinition(commodityId));
        }

        public bool TrySpendCurrency(string currencyCardId, int amount)
        {
            return MarketCurrencyService.TrySpend(
                currency,
                amount,
                BackpackService.Current,
                CardManager.Instance?.AllCards);
        }

        public bool TryAddCurrency(string currencyCardId, int amount)
        {
            BackpackData backpack = BackpackService.Current;
            if (currency == null || backpack == null || amount <= 0)
                return false;

            var previousIds = new HashSet<string>(
                backpack.Entries
                    .Where(entry => entry != null)
                    .Select(entry => entry.InstanceId));
            bool stored =
                BackpackService.TryStoreGeneratedCardsDeferred(
                    currency,
                    amount,
                    backpack);
            List<string> addedIds = FindNewEntryIds(
                backpack,
                previousIds);
            if (stored && addedIds.Count == amount)
                return true;

            RemoveEntries(backpack, addedIds);
            return false;
        }

        public bool TryRemoveCommodity(
            string commodityId,
            int quantity)
        {
            CardDefinition definition =
                GetDefinition(commodityId);
            BackpackData backpack = BackpackService.Current;
            if (definition == null || backpack == null || quantity <= 0)
                return false;

            List<BackpackEntryData> entries = backpack.Entries
                .Where(entry =>
                    pendingAddedEntryIds?.Contains(entry?.InstanceId) ==
                        true)
                .Take(quantity)
                .ToList();
            if (entries.Count != quantity)
            {
                entries = backpack.Entries
                    .Where(entry => entry?.Card?.Id == definition.Id)
                    .Take(quantity)
                    .ToList();
            }
            if (entries.Count != quantity)
                return false;
            var removedEntries = new List<BackpackEntryData>();
            foreach (BackpackEntryData entry in entries)
            {
                if (!backpack.TryRemove(entry.InstanceId, out _))
                {
                    foreach (BackpackEntryData removed in removedEntries)
                        backpack.Entries.Add(removed);
                    backpack.Normalize();
                    BackpackService.NotifyContentsChanged();
                    return false;
                }
                removedEntries.Add(entry);
            }
            pendingRemovedEntries = removedEntries;
            BackpackService.NotifyContentsChanged();
            pendingAddedEntryIds = null;
            return true;
        }

        public bool TryAddCommodity(string commodityId, int quantity)
        {
            CommodityDefinition commodity =
                catalog?.GetCommodity(commodityId);
            CardDefinition definition = GetDefinition(commodityId);
            BackpackData backpack = BackpackService.Current;
            if (commodity == null ||
                definition == null ||
                quantity <= 0 ||
                backpack == null)
            {
                return false;
            }

            if (pendingRemovedEntries?.Count == quantity &&
                pendingRemovedEntries.All(entry =>
                    entry?.Card?.Id == definition.Id))
            {
                foreach (BackpackEntryData entry in pendingRemovedEntries)
                    backpack.Entries.Add(entry);
                backpack.Normalize();
                pendingRemovedEntries = null;
                BackpackService.NotifyContentsChanged();
                return true;
            }

            var previousIds = new HashSet<string>(
                backpack.Entries
                    .Where(entry => entry != null)
                    .Select(entry => entry.InstanceId));
            bool stored =
                BackpackService.TryStoreGeneratedCardsDeferred(
                    definition,
                    quantity,
                    backpack);

            pendingAddedEntryIds = FindNewEntryIds(
                backpack,
                previousIds);
            if (stored && pendingAddedEntryIds.Count == quantity)
                return true;

            RemoveEntries(backpack, pendingAddedEntryIds);
            pendingAddedEntryIds = null;
            return false;
        }

        public bool TryPrepareTrade(
            MarketTradeDirection direction,
            string currencyCardId,
            string commodityId,
            int quantity,
            int totalPrice,
            out IPlayerTradeTransaction transaction)
        {
            transaction = null;
            BackpackData backpack = BackpackService.Current;
            CardDefinition definition = GetDefinition(commodityId);
            if (backpack == null ||
                definition == null ||
                currency == null ||
                quantity <= 0 ||
                totalPrice <= 0)
            {
                return false;
            }

            transaction = new BackpackTradeTransaction(
                direction,
                backpack,
                currency,
                definition,
                quantity,
                totalPrice,
                CardManager.Instance?.AllCards);
            return true;
        }

        private CardDefinition GetDefinition(string commodityId)
        {
            return commodityId == overrideCommodityId &&
                   overrideDefinition != null
                ? overrideDefinition
                : catalog?.GetCommodity(commodityId)?.CardDefinition;
        }

        private static List<string> FindNewEntryIds(
            BackpackData backpack,
            ISet<string> previousIds)
        {
            return backpack.Entries
                .Where(entry => entry != null &&
                    !previousIds.Contains(entry.InstanceId))
                .Select(entry => entry.InstanceId)
                .ToList();
        }

        private static void RemoveEntries(
            BackpackData backpack,
            IEnumerable<string> entryIds)
        {
            foreach (string entryId in entryIds)
                backpack.TryRemove(entryId, out _);
            BackpackService.NotifyContentsChanged();
        }

        private sealed class BackpackTradeTransaction :
            IPlayerTradeTransaction
        {
            private readonly MarketTradeDirection direction;
            private readonly BackpackData backpack;
            private readonly CardDefinition currency;
            private readonly CardDefinition commodity;
            private readonly int quantity;
            private readonly int totalPrice;
            private readonly List<BackpackEntryData> originalEntries;
            private readonly int originalCapacity;
            private readonly List<CardInstance> tableCoins;
            private bool completed;

            public BackpackTradeTransaction(
                MarketTradeDirection direction,
                BackpackData backpack,
                CardDefinition currency,
                CardDefinition commodity,
                int quantity,
                int totalPrice,
                IEnumerable<CardInstance> worldCards)
            {
                this.direction = direction;
                this.backpack = backpack;
                this.currency = currency;
                this.commodity = commodity;
                this.quantity = quantity;
                this.totalPrice = totalPrice;
                originalEntries = new List<BackpackEntryData>(
                    backpack.Entries);
                originalCapacity = backpack.SlotCapacity;

                int storedCoinCount = originalEntries.Count(entry =>
                    entry?.Card?.Id == currency.Id);
                int tableCoinCount = Math.Max(
                    0,
                    totalPrice - storedCoinCount);
                tableCoins = worldCards?
                    .Where(card =>
                        card != null &&
                        card.Stack != null &&
                        card.BaseDefinition == currency)
                    .Take(tableCoinCount)
                    .ToList() ?? new List<CardInstance>();
            }

            public bool Commit()
            {
                if (completed)
                    return false;

                try
                {
                    if (direction ==
                        MarketTradeDirection.PlayerBuys)
                    {
                        if (!CommitPurchase())
                        {
                            RestoreBackpack();
                            return false;
                        }
                    }
                    else if (!CommitSale())
                    {
                        RestoreBackpack();
                        return false;
                    }

                    BackpackService.NotifyContentsChanged();
                    foreach (CardInstance tableCoin in tableCoins)
                        tableCoin?.Stack?
                            .DestroyCardForCommittedTrade(tableCoin);
                    completed = true;
                    return true;
                }
                catch
                {
                    RestoreBackpack();
                    throw;
                }
            }

            public void Rollback()
            {
                if (!completed)
                    RestoreBackpack();
            }

            private bool CommitPurchase()
            {
                int storedCoinsToSpend = Math.Min(
                    totalPrice,
                    originalEntries.Count(entry =>
                        entry?.Card?.Id == currency.Id));
                if (storedCoinsToSpend + tableCoins.Count <
                    totalPrice)
                {
                    return false;
                }
                if (!BackpackService.TryStoreGeneratedCardsDeferred(
                        commodity,
                        quantity,
                        backpack))
                {
                    return false;
                }

                foreach (BackpackEntryData entry in originalEntries
                             .Where(entry =>
                                 entry?.Card?.Id == currency.Id)
                             .Take(storedCoinsToSpend))
                {
                    if (!backpack.TryRemove(
                            entry.InstanceId,
                            out _))
                    {
                        return false;
                    }
                }
                return true;
            }

            private bool CommitSale()
            {
                List<BackpackEntryData> goods = originalEntries
                    .Where(entry =>
                        entry?.Card?.Id == commodity.Id)
                    .Take(quantity)
                    .ToList();
                if (goods.Count != quantity)
                    return false;
                foreach (BackpackEntryData entry in goods)
                {
                    if (!backpack.TryRemove(
                            entry.InstanceId,
                            out _))
                    {
                        return false;
                    }
                }
                return BackpackService
                    .TryStoreGeneratedCardsDeferred(
                        currency,
                        totalPrice,
                        backpack);
            }

            private void RestoreBackpack()
            {
                backpack.Entries = new List<BackpackEntryData>(
                    originalEntries);
                backpack.SlotCapacity = originalCapacity;
                backpack.Normalize();
            }
        }
    }
}
