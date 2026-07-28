using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    internal sealed class WorldStackTradeInventory :
        IAtomicPlayerTradeInventory
    {
        private readonly CardDefinition currency;
        private readonly string commodityId;
        private readonly CardStack stack;
        private bool reserved;

        public WorldStackTradeInventory(
            CardDefinition currency,
            string commodityId,
            CardStack stack)
        {
            this.currency = currency;
            this.commodityId = commodityId;
            this.stack = stack;
        }

        public int CountCurrency(string currencyCardId)
        {
            return 0;
        }

        public int CountCommodity(string requestedCommodityId)
        {
            return requestedCommodityId == commodityId
                ? stack?.Cards?.Count ?? 0
                : 0;
        }

        public bool CanReceive(string requestedCommodityId, int amount)
        {
            return false;
        }

        public bool TrySpendCurrency(string currencyCardId, int amount)
        {
            return false;
        }

        public bool TryAddCurrency(string currencyCardId, int amount)
        {
            BackpackData backpack = BackpackService.Current;
            if (!reserved ||
                currency == null ||
                backpack == null ||
                amount <= 0)
            {
                return false;
            }

            var previousIds = new HashSet<string>(
                backpack.Entries
                    .Where(entry => entry != null)
                    .Select(entry => entry.InstanceId));
            bool stored =
                BackpackService.TryStoreGeneratedCardsDeferred(
                    currency,
                    amount,
                    backpack);
            List<string> addedIds = backpack.Entries
                .Where(entry => entry != null &&
                    !previousIds.Contains(entry.InstanceId))
                .Select(entry => entry.InstanceId)
                .ToList();
            if (stored && addedIds.Count == amount)
                return true;

            foreach (string entryId in addedIds)
                backpack.TryRemove(entryId, out _);
            BackpackService.NotifyContentsChanged();
            return false;
        }

        public bool TryRemoveCommodity(
            string requestedCommodityId,
            int amount)
        {
            if (reserved ||
                requestedCommodityId != commodityId ||
                amount != CountCommodity(requestedCommodityId))
            {
                return false;
            }
            reserved = true;
            return true;
        }

        public bool TryAddCommodity(
            string requestedCommodityId,
            int amount)
        {
            if (!reserved ||
                requestedCommodityId != commodityId ||
                amount != CountCommodity(requestedCommodityId))
            {
                return false;
            }
            reserved = false;
            return true;
        }

        public bool TryPrepareTrade(
            MarketTradeDirection direction,
            string currencyCardId,
            string requestedCommodityId,
            int amount,
            int totalPrice,
            out IPlayerTradeTransaction transaction)
        {
            transaction = null;
            if (direction != MarketTradeDirection.PlayerSells ||
                requestedCommodityId != commodityId ||
                amount != CountCommodity(requestedCommodityId) ||
                amount <= 0)
            {
                return false;
            }

            BackpackData backpack = BackpackService.Current;
            if (backpack == null || currency == null)
                return false;
            transaction = new WorldStackTradeTransaction(
                this,
                backpack,
                currency,
                stack,
                amount,
                totalPrice);
            return true;
        }

        private static void RemoveNewCurrency(
            BackpackData backpack,
            ISet<string> previousIds)
        {
            foreach (string entryId in backpack.Entries
                         .Where(entry => entry != null &&
                             !previousIds.Contains(entry.InstanceId))
                         .Select(entry => entry.InstanceId)
                         .ToList())
            {
                backpack.TryRemove(entryId, out _);
            }
            BackpackService.NotifyContentsChanged();
        }

        private sealed class WorldStackTradeTransaction :
            IPlayerTradeTransaction
        {
            private readonly WorldStackTradeInventory owner;
            private readonly BackpackData backpack;
            private readonly CardDefinition currency;
            private readonly CardStack stack;
            private readonly int amount;
            private readonly int totalPrice;
            private readonly List<BackpackEntryData> originalEntries;
            private readonly int originalCapacity;
            private readonly List<CardInstance> cards;
            private bool completed;

            public WorldStackTradeTransaction(
                WorldStackTradeInventory owner,
                BackpackData backpack,
                CardDefinition currency,
                CardStack stack,
                int amount,
                int totalPrice)
            {
                this.owner = owner;
                this.backpack = backpack;
                this.currency = currency;
                this.stack = stack;
                this.amount = amount;
                this.totalPrice = totalPrice;
                originalEntries = new List<BackpackEntryData>(
                    backpack.Entries);
                originalCapacity = backpack.SlotCapacity;
                cards = stack.Cards.ToList();
            }

            public bool Commit()
            {
                if (completed ||
                    cards.Count != amount ||
                    stack.Cards.Count != amount ||
                    cards.Any(card =>
                        card == null || card.Stack != stack))
                {
                    return false;
                }

                try
                {
                    if (!BackpackService
                            .TryStoreGeneratedCardsDeferred(
                                currency,
                                totalPrice,
                                backpack))
                    {
                        RestoreBackpack();
                        return false;
                    }
                    BackpackService.NotifyContentsChanged();
                    TradeManager.Instance?.NotifyCardsSold(stack);
                }
                catch
                {
                    RestoreBackpack();
                    throw;
                }

                foreach (CardInstance card in cards)
                    stack.DestroyCardForCommittedTrade(card);
                owner.reserved = false;
                completed = true;
                return true;
            }

            public void Rollback()
            {
                if (!completed)
                {
                    RestoreBackpack();
                    owner.reserved = false;
                }
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
