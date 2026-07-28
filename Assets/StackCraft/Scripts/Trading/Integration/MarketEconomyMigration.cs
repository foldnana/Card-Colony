using System;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public readonly struct LegacyMarketCommodityMapping
    {
        public LegacyMarketCommodityMapping(
            string commodityId,
            string legacyOfferId)
        {
            CommodityId = commodityId;
            LegacyOfferId = legacyOfferId;
        }

        public string CommodityId { get; }
        public string LegacyOfferId { get; }
    }

    public static class MarketEconomyMigration
    {
        public static bool MigrateRiverbendLegacy(
            GameData gameData,
            MarketStateData state,
            SceneData sceneData,
            string npcId,
            int day,
            int startingFunds,
            IEnumerable<LegacyMarketCommodityMapping> mappings)
        {
            if (gameData == null ||
                state == null ||
                state.MarketId != "riverbend-market" ||
                sceneData == null ||
                sceneData.SceneName != "Location/riverbend-market" ||
                gameData.EconomyStateVersion >=
                    GameData.CurrentEconomyStateVersion)
            {
                return false;
            }

            List<LegacyMarketCommodityMapping> resolvedMappings =
                (mappings ??
                 Array.Empty<LegacyMarketCommodityMapping>())
                .Where(mapping =>
                    !string.IsNullOrWhiteSpace(mapping.CommodityId) &&
                    !string.IsNullOrWhiteSpace(mapping.LegacyOfferId))
                .ToList();
            if (resolvedMappings.Count == 0)
                return false;

            NpcTradeStateData legacyFunds =
                sceneData?.NpcTrades?.FirstOrDefault(item =>
                    item != null &&
                    item.NpcId == npcId &&
                    item.Day == day);
            if (legacyFunds != null)
                state.AvailableFunds = legacyFunds.AvailableFunds;

            var totals = new Dictionary<string, int>();
            foreach (LegacyMarketCommodityMapping mapping in
                     resolvedMappings)
            {
                MarketStockData legacy =
                    sceneData?.MarketStock?.FirstOrDefault(item =>
                        item != null &&
                        (item.OfferId == mapping.LegacyOfferId ||
                         item.OfferId ==
                            $"npc:{npcId}:{mapping.LegacyOfferId}"));
                if (legacy == null ||
                    string.IsNullOrWhiteSpace(mapping.CommodityId))
                {
                    continue;
                }

                totals.TryGetValue(
                    mapping.CommodityId,
                    out int current);
                totals[mapping.CommodityId] = SaturatingAdd(
                    current,
                    Math.Max(0, legacy.Remaining));
            }

            foreach (var pair in totals)
            {
                MarketCommodityStateData commodity =
                    state.GetCommodity(pair.Key);
                if (commodity != null)
                    commodity.Stock = pair.Value;
            }

            state.StateRevision++;
            state.StateVersion =
                MarketRefreshEngine.CurrentStateVersion;
            gameData.EconomyStateVersion =
                GameData.CurrentEconomyStateVersion;
            return true;
        }

        private static int SaturatingAdd(int left, int right)
        {
            return (int)Math.Min(
                int.MaxValue,
                (long)left + right);
        }
    }
}
