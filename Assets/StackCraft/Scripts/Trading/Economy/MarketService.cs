using System;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public interface IMarketCatalog
    {
        CommodityDefinition GetCommodity(string commodityId);
        MarketProfile GetMarket(string marketId);
    }

    public interface IMarketStateRepository
    {
        MarketStateData Get(string marketId);
        void Add(MarketStateData state);
        void MarkChanged(MarketStateData state);
    }

    public interface IWorldClock
    {
        long CurrentWorldHour { get; }
    }

    public interface IPlayerTradeInventory
    {
        int CountCurrency(string currencyCardId);
        int CountCommodity(string commodityId);
        bool CanReceive(string commodityId, int quantity);
        bool TrySpendCurrency(string currencyCardId, int amount);
        bool TryAddCurrency(string currencyCardId, int amount);
        bool TryRemoveCommodity(string commodityId, int quantity);
        bool TryAddCommodity(string commodityId, int quantity);
    }

    public interface IAtomicPlayerTradeInventory :
        IPlayerTradeInventory
    {
        bool TryPrepareTrade(
            MarketTradeDirection direction,
            string currencyCardId,
            string commodityId,
            int quantity,
            int totalPrice,
            out IPlayerTradeTransaction transaction);
    }

    public interface IPlayerTradeTransaction
    {
        bool Commit();
        void Rollback();
    }

    public sealed class MarketService
    {
        private readonly IMarketCatalog catalog;
        private readonly IWorldClock worldClock;
        private readonly IMarketStateRepository repository;
        private readonly MarketQuoteCalculator quoteCalculator;
        private readonly MarketRefreshEngine refreshEngine;
        private readonly int economySeed;
        private readonly Dictionary<string, MerchantPolicy> merchantPolicies =
            new();

        public MarketService(
            IMarketCatalog catalog,
            IWorldClock worldClock,
            IMarketStateRepository repository,
            int economySeed)
            : this(
                catalog,
                worldClock,
                repository,
                new MarketQuoteCalculator(),
                new MarketRefreshEngine(),
                economySeed)
        {
        }

        public MarketService(
            IMarketCatalog catalog,
            IWorldClock worldClock,
            IMarketStateRepository repository,
            MarketQuoteCalculator quoteCalculator,
            MarketRefreshEngine refreshEngine,
            int economySeed)
        {
            this.catalog = catalog ??
                throw new ArgumentNullException(nameof(catalog));
            this.worldClock = worldClock ??
                throw new ArgumentNullException(nameof(worldClock));
            this.repository = repository ??
                throw new ArgumentNullException(nameof(repository));
            this.quoteCalculator = quoteCalculator ??
                throw new ArgumentNullException(nameof(quoteCalculator));
            this.refreshEngine = refreshEngine ??
                throw new ArgumentNullException(nameof(refreshEngine));
            this.economySeed = economySeed;
        }

        public MarketStateData GetOrCreateState(string marketId)
        {
            MarketProfile profile = catalog.GetMarket(marketId);
            if (profile == null)
                return null;

            MarketStateData state = repository.Get(marketId);
            if (state == null)
            {
                state = new MarketStateData();
                refreshEngine.Initialize(
                    profile,
                    state,
                    worldClock.CurrentWorldHour,
                    economySeed);
                repository.Add(state);
            }
            else
            {
                int revision = state.StateRevision;
                refreshEngine.CatchUp(
                    profile,
                    state,
                    worldClock.CurrentWorldHour,
                    economySeed);
                if (state.StateRevision != revision)
                    repository.MarkChanged(state);
            }

            return state;
        }

        public void RegisterMerchantPolicy(
            string merchantId,
            MerchantTradeFilter filter,
            MerchantPriceModifiers modifiers)
        {
            if (string.IsNullOrWhiteSpace(merchantId))
                throw new ArgumentException(
                    "Merchant id cannot be empty.",
                    nameof(merchantId));
            merchantPolicies[merchantId] =
                new MerchantPolicy(filter, modifiers);
        }

        public MarketQuote GetQuote(
            string marketId,
            string commodityId,
            MerchantPriceModifiers modifiers)
        {
            MarketProfile profile = catalog.GetMarket(marketId);
            MarketCommodityRule rule = profile?.GetRule(commodityId);
            MarketStateData state = GetOrCreateState(marketId);
            return rule == null || state == null
                ? default
                : quoteCalculator.Calculate(
                    profile,
                    rule,
                    state,
                    modifiers,
                    worldClock.CurrentWorldHour);
        }

        public IReadOnlyList<MarketQuote> GetQuotes(
            string marketId,
            MerchantTradeFilter filter,
            MerchantPriceModifiers modifiers)
        {
            MarketProfile profile = catalog.GetMarket(marketId);
            MarketStateData state = GetOrCreateState(marketId);
            if (profile == null || state == null)
                return Array.Empty<MarketQuote>();

            return profile.CommodityRules
                .Where(rule => rule?.Commodity != null &&
                    (filter == null ||
                     filter.Allows(
                         rule.Commodity.Id,
                         MarketTradeDirection.PlayerBuys) ||
                     filter.Allows(
                         rule.Commodity.Id,
                         MarketTradeDirection.PlayerSells)))
                .Select(rule => quoteCalculator.Calculate(
                    profile,
                    rule,
                    state,
                    modifiers,
                    worldClock.CurrentWorldHour))
                .ToList();
        }

        public MarketTradeResult Validate(
            MarketTradeRequest request,
            IPlayerTradeInventory playerInventory)
        {
            return ValidateInternal(
                request,
                playerInventory,
                out _,
                out _,
                out MarketQuote quote,
                out int total)
                ? Result(true, MarketTradeFailure.None, request, quote, total)
                : Result(
                    false,
                    validationFailure,
                    request,
                    quote,
                    total);
        }

        private MarketTradeFailure validationFailure;

        public MarketTradeResult Execute(
            MarketTradeRequest request,
            IPlayerTradeInventory playerInventory)
        {
            if (!ValidateInternal(
                    request,
                    playerInventory,
                    out MarketProfile profile,
                    out MarketCommodityStateData commodityState,
                    out MarketQuote quote,
                    out int total))
            {
                return Result(
                    false,
                    validationFailure,
                    request,
                    quote,
                    total);
            }

            if (playerInventory is not
                IAtomicPlayerTradeInventory atomic ||
                !atomic.TryPrepareTrade(
                    request.Direction,
                    profile.Currency?.Id ?? string.Empty,
                    request.CommodityId,
                    request.Quantity,
                    total,
                    out IPlayerTradeTransaction transaction) ||
                transaction == null)
            {
                return Result(
                    false,
                    MarketTradeFailure.CommitFailed,
                    request,
                    quote,
                    total);
            }

            MarketStateData committedState =
                repository.Get(request.MarketId);
            var snapshot = new MarketCommitSnapshot(
                committedState,
                commodityState);
            try
            {
                ApplyMarketTrade(
                    request,
                    total,
                    committedState,
                    commodityState);
                repository.MarkChanged(committedState);
                if (!transaction.Commit())
                    throw new InvalidOperationException(
                        "Player trade transaction rejected commit.");
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                    // The market snapshot must still be restored even if
                    // a runtime adapter reports a rollback error.
                }
                snapshot.Restore();
                try
                {
                    repository.MarkChanged(committedState);
                }
                catch
                {
                    // The in-memory state is authoritative for this frame.
                }
                return Result(
                    false,
                    MarketTradeFailure.CommitFailed,
                    request,
                    quote,
                    total);
            }

            MarketQuote updated = GetQuote(
                request.MarketId,
                request.CommodityId,
                GetTrustedModifiers(request.MerchantId));
            return Result(
                true,
                MarketTradeFailure.None,
                request,
                updated,
                total);
        }

        private bool ValidateInternal(
            MarketTradeRequest request,
            IPlayerTradeInventory inventory,
            out MarketProfile profile,
            out MarketCommodityStateData commodityState,
            out MarketQuote quote,
            out int total)
        {
            validationFailure = MarketTradeFailure.None;
            profile = catalog.GetMarket(request.MarketId);
            commodityState = null;
            quote = default;
            total = 0;
            if (profile == null)
                return Fail(MarketTradeFailure.InvalidMarket);

            if (string.IsNullOrWhiteSpace(request.MerchantId) ||
                !merchantPolicies.TryGetValue(
                    request.MerchantId,
                    out MerchantPolicy policy))
            {
                return Fail(
                    MarketTradeFailure.MerchantDoesNotTradeCommodity);
            }
            MerchantTradeFilter trustedFilter = policy.Filter;
            MerchantPriceModifiers trustedModifiers = policy.Modifiers;

            MarketCommodityRule rule =
                profile.GetRule(request.CommodityId);
            if (rule?.Commodity == null ||
                catalog.GetCommodity(request.CommodityId) == null)
            {
                return Fail(MarketTradeFailure.InvalidCommodity);
            }
            if (trustedFilter != null &&
                !trustedFilter.Allows(
                    request.CommodityId,
                    request.Direction))
            {
                return Fail(
                    MarketTradeFailure.MerchantDoesNotTradeCommodity);
            }
            if ((request.Direction ==
                    MarketTradeDirection.PlayerBuys &&
                 !rule.AllowsPlayerPurchase) ||
                (request.Direction ==
                    MarketTradeDirection.PlayerSells &&
                 !rule.AllowsPlayerSale))
            {
                return Fail(MarketTradeFailure.DirectionClosed);
            }
            if (request.Quantity <= 0)
                return Fail(MarketTradeFailure.InvalidQuantity);
            if (inventory == null)
                return Fail(MarketTradeFailure.CommitFailed);

            MarketStateData state = GetOrCreateState(request.MarketId);
            if (state.StateRevision >= int.MaxValue)
                return Fail(MarketTradeFailure.CommitFailed);
            commodityState = state.GetCommodity(request.CommodityId);
            quote = quoteCalculator.Calculate(
                profile,
                rule,
                state,
                trustedModifiers,
                worldClock.CurrentWorldHour);
            int price = request.Direction ==
                MarketTradeDirection.PlayerBuys
                    ? quote.PlayerBuyUnitPrice
                    : quote.PlayerSellUnitPrice;
            if (request.ExpectedStateRevision != quote.StateRevision ||
                request.ExpectedUnitPrice != price)
            {
                return Fail(MarketTradeFailure.StaleQuote);
            }
            long longTotal = (long)price * request.Quantity;
            if (longTotal <= 0 || longTotal > int.MaxValue)
                return Fail(MarketTradeFailure.InvalidQuantity);
            total = (int)longTotal;

            if (request.Direction == MarketTradeDirection.PlayerBuys)
            {
                if (commodityState.Stock < request.Quantity)
                    return Fail(
                        MarketTradeFailure.InsufficientMarketStock);
                if (inventory.CountCurrency(
                        profile.Currency?.Id ?? string.Empty) < total)
                {
                    return Fail(
                        MarketTradeFailure.InsufficientPlayerCurrency);
                }
                if (!inventory.CanReceive(
                        request.CommodityId,
                        request.Quantity))
                {
                    return Fail(
                        MarketTradeFailure.InventoryCannotReceive);
                }
                if ((long)state.AvailableFunds + total >
                    profile.MaximumFunds)
                {
                    return Fail(
                        MarketTradeFailure.MarketFundsCapacityReached);
                }
            }
            else
            {
                if (inventory.CountCommodity(request.CommodityId) <
                    request.Quantity)
                {
                    return Fail(
                        MarketTradeFailure.InsufficientPlayerGoods);
                }
                if (state.AvailableFunds < total)
                    return Fail(
                        MarketTradeFailure.InsufficientMarketFunds);
                if ((long)commodityState.Stock + request.Quantity >
                    rule.MaximumStock)
                {
                    return Fail(
                        MarketTradeFailure.MarketStockCapacityReached);
                }
            }

            return true;
        }

        private bool Fail(MarketTradeFailure failure)
        {
            validationFailure = failure;
            return false;
        }

        private void ApplyMarketTrade(
            MarketTradeRequest request,
            int total,
            MarketStateData state,
            MarketCommodityStateData commodity)
        {
            if (request.Direction == MarketTradeDirection.PlayerBuys)
            {
                commodity.Stock -= request.Quantity;
                commodity.RecentPlayerPurchaseQuantity +=
                    request.Quantity;
                state.AvailableFunds += total;
            }
            else
            {
                state.AvailableFunds -= total;
                commodity.Stock += request.Quantity;
                commodity.RecentPlayerSaleQuantity +=
                    request.Quantity;
            }
            commodity.LastTradeWorldHour =
                worldClock.CurrentWorldHour;
            if (state.StateRevision >= int.MaxValue)
                throw new InvalidOperationException(
                    "Market state revision is exhausted.");
            state.StateRevision++;
        }

        private static MarketTradeResult Result(
            bool success,
            MarketTradeFailure failure,
            MarketTradeRequest request,
            MarketQuote quote,
            int total)
        {
            int price = request.Direction ==
                MarketTradeDirection.PlayerBuys
                    ? quote.PlayerBuyUnitPrice
                    : quote.PlayerSellUnitPrice;
            return new MarketTradeResult(
                success,
                failure,
                request.Quantity,
                price,
                total,
                quote);
        }

        private static int SaturatingAdd(
            int value,
            int addition,
            int maximum)
        {
            return (int)Math.Min(
                maximum,
                Math.Max(0L, (long)value + addition));
        }

        private MerchantPriceModifiers GetTrustedModifiers(
            string merchantId)
        {
            return !string.IsNullOrWhiteSpace(merchantId) &&
                   merchantPolicies.TryGetValue(
                       merchantId,
                       out MerchantPolicy policy)
                ? policy.Modifiers
                : MerchantPriceModifiers.Default;
        }

        private sealed class MerchantPolicy
        {
            public MerchantPolicy(
                MerchantTradeFilter filter,
                MerchantPriceModifiers modifiers)
            {
                Filter = filter;
                Modifiers = modifiers;
            }

            public MerchantTradeFilter Filter { get; }
            public MerchantPriceModifiers Modifiers { get; }
        }

        private readonly struct MarketCommitSnapshot
        {
            private readonly MarketStateData state;
            private readonly MarketCommodityStateData commodity;
            private readonly int funds;
            private readonly int revision;
            private readonly int stock;
            private readonly int purchases;
            private readonly int sales;
            private readonly long lastTradeHour;

            public MarketCommitSnapshot(
                MarketStateData state,
                MarketCommodityStateData commodity)
            {
                this.state = state;
                this.commodity = commodity;
                funds = state.AvailableFunds;
                revision = state.StateRevision;
                stock = commodity.Stock;
                purchases =
                    commodity.RecentPlayerPurchaseQuantity;
                sales = commodity.RecentPlayerSaleQuantity;
                lastTradeHour = commodity.LastTradeWorldHour;
            }

            public void Restore()
            {
                state.AvailableFunds = funds;
                state.StateRevision = revision;
                commodity.Stock = stock;
                commodity.RecentPlayerPurchaseQuantity =
                    purchases;
                commodity.RecentPlayerSaleQuantity = sales;
                commodity.LastTradeWorldHour = lastTradeHour;
            }
        }
    }
}
