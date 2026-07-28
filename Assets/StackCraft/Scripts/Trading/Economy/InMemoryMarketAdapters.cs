using System;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public sealed class InMemoryMarketCatalog : IMarketCatalog
    {
        private readonly Dictionary<string, CommodityDefinition> commodities;
        private readonly Dictionary<string, MarketProfile> markets;

        public InMemoryMarketCatalog(
            IEnumerable<CommodityDefinition> commodities,
            IEnumerable<MarketProfile> markets)
        {
            this.commodities = (commodities ??
                    Array.Empty<CommodityDefinition>())
                .Where(item => item != null)
                .ToDictionary(item => item.Id);
            this.markets = (markets ?? Array.Empty<MarketProfile>())
                .Where(item => item != null)
                .ToDictionary(item => item.Id);
        }

        public CommodityDefinition GetCommodity(string commodityId)
        {
            commodities.TryGetValue(commodityId ?? string.Empty, out var item);
            return item;
        }

        public MarketProfile GetMarket(string marketId)
        {
            markets.TryGetValue(marketId ?? string.Empty, out var item);
            return item;
        }
    }

    public sealed class InMemoryMarketStateRepository :
        IMarketStateRepository
    {
        private readonly Dictionary<string, MarketStateData> states = new();

        public MarketStateData Get(string marketId)
        {
            states.TryGetValue(marketId ?? string.Empty, out var state);
            return state;
        }

        public void Add(MarketStateData state)
        {
            if (state != null)
                states[state.MarketId] = state;
        }

        public void MarkChanged(MarketStateData state)
        {
        }
    }

    public sealed class MutableWorldClock : IWorldClock
    {
        public MutableWorldClock(long currentWorldHour)
        {
            CurrentWorldHour = currentWorldHour;
        }

        public long CurrentWorldHour { get; set; }
    }

    public sealed class InMemoryPlayerTradeInventory :
        IAtomicPlayerTradeInventory
    {
        private readonly Dictionary<string, int> commodities;
        private readonly int capacity;

        public InMemoryPlayerTradeInventory(
            int currency,
            IDictionary<string, int> commodities,
            int capacity)
        {
            Currency = Math.Max(0, currency);
            this.commodities = commodities == null
                ? new Dictionary<string, int>()
                : new Dictionary<string, int>(commodities);
            this.capacity = Math.Max(0, capacity);
        }

        public int Currency { get; private set; }
        public bool FailNextAddCurrency { get; set; }
        public bool ThrowAfterMutationOnNextCommit { get; set; }

        public int CountCurrency(string currencyCardId)
        {
            return Currency;
        }

        public int CountCommodity(string commodityId)
        {
            return commodities.TryGetValue(
                commodityId ?? string.Empty,
                out int quantity)
                    ? quantity
                    : 0;
        }

        public bool CanReceive(string commodityId, int quantity)
        {
            return quantity > 0 &&
                TotalCommodityCount() + quantity <= capacity;
        }

        public bool TrySpendCurrency(string currencyCardId, int amount)
        {
            if (amount <= 0 || Currency < amount)
                return false;
            Currency -= amount;
            return true;
        }

        public bool TryAddCurrency(string currencyCardId, int amount)
        {
            if (FailNextAddCurrency)
            {
                FailNextAddCurrency = false;
                return false;
            }
            if (amount <= 0 || Currency > int.MaxValue - amount)
                return false;
            Currency += amount;
            return true;
        }

        public bool TryRemoveCommodity(
            string commodityId,
            int quantity)
        {
            int current = CountCommodity(commodityId);
            if (quantity <= 0 || current < quantity)
                return false;
            commodities[commodityId] = current - quantity;
            return true;
        }

        public bool TryAddCommodity(string commodityId, int quantity)
        {
            if (!CanReceive(commodityId, quantity))
                return false;
            int current = CountCommodity(commodityId);
            if (current > int.MaxValue - quantity)
                return false;
            commodities[commodityId] = current + quantity;
            return true;
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
            if (direction == MarketTradeDirection.PlayerBuys)
            {
                if (quantity <= 0 ||
                    totalPrice <= 0 ||
                    Currency < totalPrice ||
                    !CanReceive(commodityId, quantity))
                {
                    return false;
                }
                transaction = new InMemoryTradeTransaction(
                    this,
                    direction,
                    commodityId,
                    quantity,
                    totalPrice);
                return true;
            }

            if (quantity <= 0 ||
                totalPrice <= 0 ||
                CountCommodity(commodityId) < quantity ||
                Currency > int.MaxValue - totalPrice)
            {
                return false;
            }
            transaction = new InMemoryTradeTransaction(
                this,
                direction,
                commodityId,
                quantity,
                totalPrice);
            return true;
        }

        private int TotalCommodityCount()
        {
            long total = commodities.Values.Sum(value =>
                (long)Math.Max(0, value));
            return (int)Math.Min(int.MaxValue, total);
        }

        private sealed class InMemoryTradeTransaction :
            IPlayerTradeTransaction
        {
            private readonly InMemoryPlayerTradeInventory owner;
            private readonly MarketTradeDirection direction;
            private readonly string commodityId;
            private readonly int quantity;
            private readonly int totalPrice;
            private readonly int originalCurrency;
            private readonly int originalCommodityQuantity;
            private bool committed;

            public InMemoryTradeTransaction(
                InMemoryPlayerTradeInventory owner,
                MarketTradeDirection direction,
                string commodityId,
                int quantity,
                int totalPrice)
            {
                this.owner = owner;
                this.direction = direction;
                this.commodityId = commodityId;
                this.quantity = quantity;
                this.totalPrice = totalPrice;
                originalCurrency = owner.Currency;
                originalCommodityQuantity =
                    owner.CountCommodity(commodityId);
            }

            public bool Commit()
            {
                if (committed)
                    return false;
                if (direction == MarketTradeDirection.PlayerSells &&
                    owner.FailNextAddCurrency)
                {
                    owner.FailNextAddCurrency = false;
                    return false;
                }

                owner.Currency = direction ==
                    MarketTradeDirection.PlayerBuys
                        ? originalCurrency - totalPrice
                        : originalCurrency + totalPrice;
                owner.commodities[commodityId] = direction ==
                    MarketTradeDirection.PlayerBuys
                        ? originalCommodityQuantity + quantity
                        : originalCommodityQuantity - quantity;
                committed = true;
                if (owner.ThrowAfterMutationOnNextCommit)
                {
                    owner.ThrowAfterMutationOnNextCommit = false;
                    throw new InvalidOperationException(
                        "Simulated inventory notification failure.");
                }
                return true;
            }

            public void Rollback()
            {
                owner.Currency = originalCurrency;
                owner.commodities[commodityId] =
                    originalCommodityQuantity;
                committed = false;
            }
        }
    }
}
