using System;

namespace CryingSnow.StackCraft
{
    public sealed class MarketQuoteCalculator
    {
        public MarketQuote Calculate(
            MarketProfile profile,
            MarketCommodityRule rule,
            MarketStateData state,
            MerchantPriceModifiers modifiers,
            long worldHour,
            float eventFactor = 1f)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (rule?.Commodity == null)
                throw new ArgumentNullException(nameof(rule));
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            MarketCommodityStateData commodityState =
                state.GetCommodity(rule.Commodity.Id);
            int stock = Math.Max(0, commodityState?.Stock ?? 0);
            double inventoryRatio =
                (double)stock / Math.Max(1, rule.TargetStock);
            double inventoryFactor = Clamp(
                1d + rule.Commodity.Volatility *
                    (1d - inventoryRatio),
                0.75d,
                1.45d);
            double normalMidpoint =
                rule.Commodity.BasePrice * rule.RegionalPriceFactor;
            double midpoint = normalMidpoint *
                inventoryFactor *
                Math.Max(0.01f, eventFactor);
            double halfSpread = profile.BaseSpread / 2d;

            int buy = Math.Max(
                1,
                (int)Math.Ceiling(
                    midpoint *
                    (1d + halfSpread) *
                    modifiers.SellToPlayer *
                    (1d + profile.TransactionFee)));
            int sell = Math.Max(
                1,
                (int)Math.Floor(
                    midpoint *
                    (1d - halfSpread) *
                    modifiers.BuyFromPlayer));
            if (buy <= sell)
                buy = sell + 1;

            double trendRatio = normalMidpoint <= 0d
                ? 1d
                : midpoint / normalMidpoint;
            MarketTrend trend = trendRatio < 0.88d
                ? MarketTrend.Abundant
                : trendRatio <= 1.18d
                    ? MarketTrend.Normal
                    : trendRatio <= 1.5d
                        ? MarketTrend.Shortage
                        : MarketTrend.Emergency;
            int affordable = sell <= 0
                ? 0
                : Math.Min(
                    Math.Max(0, state.AvailableFunds) / sell,
                    Math.Max(0, rule.MaximumStock - stock));

            return new MarketQuote(
                profile.Id,
                rule.Commodity.Id,
                buy,
                sell,
                stock,
                affordable,
                trend,
                worldHour,
                state.StateRevision);
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return value < minimum
                ? minimum
                : value > maximum
                    ? maximum
                    : value;
        }
    }
}
