using System;
using System.Collections.Generic;

namespace CryingSnow.StackCraft
{
    public sealed class LocalMarketTradeSession
    {
        private readonly MarketService marketService;

        public LocalMarketTradeSession(
            LocalMarketContext context,
            MarketService marketService)
        {
            Context = context ??
                throw new ArgumentNullException(nameof(context));
            this.marketService = marketService ??
                throw new ArgumentNullException(nameof(marketService));
        }

        public LocalMarketContext Context { get; }
        public MarketProfile MarketProfile => Context.MarketProfile;

        public IReadOnlyList<MarketQuote> GetQuotes()
        {
            return marketService.GetQuotes(
                MarketProfile.Id,
                null,
                MerchantPriceModifiers.Default);
        }

        public MarketQuote GetQuote(string commodityId)
        {
            return marketService.GetQuote(
                MarketProfile.Id,
                commodityId,
                MerchantPriceModifiers.Default);
        }

        public bool AllowsDirection(
            string commodityId,
            MarketTradeDirection direction)
        {
            MarketCommodityRule rule =
                MarketProfile.GetRule(commodityId);
            return rule?.Commodity != null &&
                (direction == MarketTradeDirection.PlayerBuys
                    ? rule.AllowsPlayerPurchase
                    : rule.AllowsPlayerSale);
        }

        public bool IsQuoteStale(MarketQuote quote)
        {
            if (!string.Equals(
                    quote.MarketId,
                    MarketProfile.Id,
                    StringComparison.Ordinal))
            {
                return false;
            }

            MarketStateData state =
                marketService.GetOrCreateState(MarketProfile.Id);
            return state == null ||
                state.StateRevision != quote.StateRevision;
        }

        public int GetMaximumQuantity(
            MarketQuote quote,
            MarketTradeDirection direction,
            IPlayerTradeInventory playerInventory)
        {
            if (playerInventory == null ||
                string.IsNullOrWhiteSpace(quote.CommodityId) ||
                !string.Equals(
                    quote.MarketId,
                    MarketProfile.Id,
                    StringComparison.Ordinal) ||
                !AllowsDirection(quote.CommodityId, direction))
            {
                return 0;
            }

            MarketStateData state =
                marketService.GetOrCreateState(MarketProfile.Id);
            MarketCommodityRule rule =
                MarketProfile.GetRule(quote.CommodityId);
            if (state == null ||
                rule == null ||
                state.StateRevision != quote.StateRevision)
            {
                return 0;
            }

            int unitPrice = direction ==
                MarketTradeDirection.PlayerBuys
                    ? quote.PlayerBuyUnitPrice
                    : quote.PlayerSellUnitPrice;
            if (unitPrice <= 0)
                return 0;

            if (direction == MarketTradeDirection.PlayerSells)
            {
                int storageRemaining = Math.Max(
                    0,
                    rule.MaximumStock - quote.AvailableStock);
                return Math.Max(
                    0,
                    Math.Min(
                        playerInventory.CountCommodity(
                            quote.CommodityId),
                        Math.Min(
                            quote.MarketAffordableQuantity,
                            storageRemaining)));
            }

            int marketFundCapacity = Math.Max(
                0,
                MarketProfile.MaximumFunds -
                state.AvailableFunds);
            int maximum = Math.Max(
                0,
                Math.Min(
                    quote.AvailableStock,
                    Math.Min(
                        playerInventory.CountCurrency(
                            MarketProfile.Currency?.Id ??
                            string.Empty) / unitPrice,
                        marketFundCapacity / unitPrice)));
            return FindReceivableQuantity(
                playerInventory,
                quote.CommodityId,
                maximum);
        }

        public MarketTradeResult Execute(
            MarketQuote quote,
            MarketTradeDirection direction,
            int quantity,
            IPlayerTradeInventory playerInventory)
        {
            if (!string.Equals(
                    quote.MarketId,
                    MarketProfile.Id,
                    StringComparison.Ordinal))
            {
                int rejectedUnitPrice = direction ==
                    MarketTradeDirection.PlayerBuys
                        ? quote.PlayerBuyUnitPrice
                        : quote.PlayerSellUnitPrice;
                return new MarketTradeResult(
                    false,
                    MarketTradeFailure.InvalidMarket,
                    quantity,
                    rejectedUnitPrice,
                    0,
                    quote);
            }

            int expectedUnitPrice = direction ==
                MarketTradeDirection.PlayerBuys
                    ? quote.PlayerBuyUnitPrice
                    : quote.PlayerSellUnitPrice;
            MarketTradeRequest request =
                MarketTradeRequest.ForPublicMarket(
                    MarketProfile.Id,
                    quote.CommodityId,
                    direction,
                    quantity,
                    expectedUnitPrice,
                    quote.StateRevision);
            return marketService.Execute(request, playerInventory);
        }

        private static int FindReceivableQuantity(
            IPlayerTradeInventory playerInventory,
            string commodityId,
            int maximum)
        {
            if (maximum <= 0 ||
                !playerInventory.CanReceive(commodityId, 1))
            {
                return 0;
            }

            int low = 1;
            int high = maximum;
            int result = 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                if (playerInventory.CanReceive(
                        commodityId,
                        middle))
                {
                    result = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }
            return result;
        }
    }
}
