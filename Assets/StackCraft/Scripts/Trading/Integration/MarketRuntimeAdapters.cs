using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public sealed class ResourcesMarketCatalog : IMarketCatalog
    {
        private readonly Dictionary<string, CommodityDefinition> commodities;
        private readonly Dictionary<string, MarketProfile> markets;

        public ResourcesMarketCatalog()
        {
            commodities = Resources
                .LoadAll<CommodityDefinition>("Trading/Commodities")
                .Where(item => item != null &&
                    !string.IsNullOrWhiteSpace(item.Id))
                .ToDictionary(item => item.Id);
            markets = Resources
                .LoadAll<MarketProfile>("Trading/Markets")
                .Where(item => item != null &&
                    !string.IsNullOrWhiteSpace(item.Id))
                .ToDictionary(item => item.Id);
        }

        public CommodityDefinition GetCommodity(string commodityId)
        {
            commodities.TryGetValue(
                commodityId ?? string.Empty,
                out CommodityDefinition commodity);
            return commodity;
        }

        public MarketProfile GetMarket(string marketId)
        {
            markets.TryGetValue(
                marketId ?? string.Empty,
                out MarketProfile market);
            return market;
        }

        public CommodityDefinition FindCommodity(
            CardDefinition definition)
        {
            if (definition == null)
                return null;

            CommodityDefinition exact = commodities.Values.FirstOrDefault(
                commodity => commodity.CardDefinition == definition ||
                    commodity.CardDefinition?.Id == definition.Id);
            if (exact != null)
                return exact;

            string id = definition.Id?.ToLowerInvariant() ?? string.Empty;
            if (id.Contains("apple") ||
                id.Contains("berry") ||
                id.Contains("potato") ||
                id.Contains("meat"))
            {
                return GetCommodity("food");
            }
            if (id.Contains("stone"))
                return GetCommodity("ore");
            return null;
        }
    }

    public sealed class GameDataMarketStateRepository :
        IMarketStateRepository
    {
        private readonly GameData gameData;

        public GameDataMarketStateRepository(GameData gameData)
        {
            this.gameData = gameData ??
                throw new ArgumentNullException(nameof(gameData));
            gameData.EnsureEconomyState();
        }

        public MarketStateData Get(string marketId)
        {
            return gameData.Markets.FirstOrDefault(state =>
                state != null && state.MarketId == marketId);
        }

        public void Add(MarketStateData state)
        {
            if (state != null && Get(state.MarketId) == null)
                gameData.Markets.Add(state);
        }

        public void MarkChanged(MarketStateData state)
        {
            gameData.EconomyStateVersion =
                GameData.CurrentEconomyStateVersion;
        }
    }

    public sealed class GameDataWorldClock : IWorldClock
    {
        private readonly GameData gameData;

        public GameDataWorldClock(GameData gameData)
        {
            this.gameData = gameData ??
                throw new ArgumentNullException(nameof(gameData));
        }

        public long CurrentWorldHour => gameData.WorldElapsedHours;
    }

    public static class MarketRuntime
    {
        private static GameData cachedGameData;
        private static ResourcesMarketCatalog cachedCatalog;
        private static MarketService cachedService;

        public static bool TryGet(
            out ResourcesMarketCatalog catalog,
            out MarketService service)
        {
            GameData gameData = GameDirector.Instance?.GameData;
            if (gameData == null)
            {
                catalog = null;
                service = null;
                return false;
            }

            gameData.EnsureEconomyState();
            if (cachedService == null || cachedGameData != gameData)
            {
                cachedGameData = gameData;
                cachedCatalog = new ResourcesMarketCatalog();
                cachedService = new MarketService(
                    cachedCatalog,
                    new GameDataWorldClock(gameData),
                    new GameDataMarketStateRepository(gameData),
                    gameData.EconomySeed);
            }

            catalog = cachedCatalog;
            service = cachedService;
            return true;
        }
    }
}
