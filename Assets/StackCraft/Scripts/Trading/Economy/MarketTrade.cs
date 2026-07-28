using System;
using System.Collections.Generic;

namespace CryingSnow.StackCraft
{
    public enum MarketTradeDirection
    {
        PlayerBuys,
        PlayerSells
    }

    public enum MarketTradeFailure
    {
        None,
        InvalidMarket,
        InvalidCommodity,
        MerchantDoesNotTradeCommodity,
        DirectionClosed,
        StaleQuote,
        InvalidQuantity,
        InsufficientMarketStock,
        InsufficientMarketFunds,
        InsufficientPlayerCurrency,
        InsufficientPlayerGoods,
        InventoryCannotReceive,
        MarketFundsCapacityReached,
        MarketStockCapacityReached,
        CommitFailed
    }

    public readonly struct MarketTradeRequest
    {
        public MarketTradeRequest(
            string marketId,
            string merchantId,
            string commodityId,
            MarketTradeDirection direction,
            int quantity,
            int expectedUnitPrice,
            int expectedStateRevision)
            : this(
                marketId,
                merchantId,
                commodityId,
                direction,
                quantity,
                expectedUnitPrice,
                expectedStateRevision,
                MerchantPriceModifiers.Default,
                null)
        {
        }

        public MarketTradeRequest(
            string marketId,
            string merchantId,
            string commodityId,
            MarketTradeDirection direction,
            int quantity,
            int expectedUnitPrice,
            int expectedStateRevision,
            MerchantPriceModifiers modifiers,
            MerchantTradeFilter filter)
        {
            MarketId = marketId;
            MerchantId = merchantId;
            CommodityId = commodityId;
            Direction = direction;
            Quantity = quantity;
            ExpectedUnitPrice = expectedUnitPrice;
            ExpectedStateRevision = expectedStateRevision;
            Modifiers = modifiers;
            Filter = filter;
        }

        public string MarketId { get; }
        public string MerchantId { get; }
        public string CommodityId { get; }
        public MarketTradeDirection Direction { get; }
        public int Quantity { get; }
        public int ExpectedUnitPrice { get; }
        public int ExpectedStateRevision { get; }
        public MerchantPriceModifiers Modifiers { get; }
        public MerchantTradeFilter Filter { get; }
    }

    public sealed class MarketTradeResult
    {
        public MarketTradeResult(
            bool success,
            MarketTradeFailure failure,
            int quantity,
            int unitPrice,
            int totalPrice,
            MarketQuote updatedQuote)
        {
            Success = success;
            Failure = failure;
            Quantity = quantity;
            UnitPrice = unitPrice;
            TotalPrice = totalPrice;
            UpdatedQuote = updatedQuote;
        }

        public bool Success { get; }
        public MarketTradeFailure Failure { get; }
        public int Quantity { get; }
        public int UnitPrice { get; }
        public int TotalPrice { get; }
        public MarketQuote UpdatedQuote { get; }
    }

    public sealed class MerchantTradeFilter
    {
        private readonly HashSet<string> sellCommodityIds;
        private readonly HashSet<string> buyCommodityIds;

        public MerchantTradeFilter(
            IEnumerable<string> sellCommodityIds = null,
            IEnumerable<string> buyCommodityIds = null)
        {
            this.sellCommodityIds = sellCommodityIds == null
                ? null
                : new HashSet<string>(sellCommodityIds);
            this.buyCommodityIds = buyCommodityIds == null
                ? null
                : new HashSet<string>(buyCommodityIds);
        }

        public bool Allows(
            string commodityId,
            MarketTradeDirection direction)
        {
            HashSet<string> ids = direction ==
                MarketTradeDirection.PlayerBuys
                    ? sellCommodityIds
                    : buyCommodityIds;
            return ids == null || ids.Contains(commodityId);
        }
    }
}
