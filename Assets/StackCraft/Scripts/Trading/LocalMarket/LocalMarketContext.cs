using System;

namespace CryingSnow.StackCraft
{
    public enum LocalMarketOpenSource
    {
        LocationButton,
        MarketBuilding,
        LocalWorldMapDetails
    }

    public sealed class LocalMarketContext
    {
        public LocalMarketContext(
            string locationId,
            string locationDisplayName,
            MarketProfile marketProfile,
            LocalMarketOpenSource openSource)
        {
            if (string.IsNullOrWhiteSpace(locationId))
                throw new ArgumentException(
                    "Location id cannot be empty.",
                    nameof(locationId));

            LocationId = locationId;
            LocationDisplayName = locationDisplayName ?? string.Empty;
            MarketProfile = marketProfile ??
                throw new ArgumentNullException(nameof(marketProfile));
            if (!string.Equals(
                    MarketProfile.LocationId,
                    LocationId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Market '{MarketProfile.Id}' belongs to location " +
                    $"'{MarketProfile.LocationId}', not '{LocationId}'.",
                    nameof(marketProfile));
            }
            OpenSource = openSource;
        }

        public string LocationId { get; }
        public string LocationDisplayName { get; }
        public MarketProfile MarketProfile { get; }
        public LocalMarketOpenSource OpenSource { get; }
    }
}
