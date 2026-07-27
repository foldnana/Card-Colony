using System;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public static class MarketStockLedger
    {
        public static MarketStockData GetOrRefreshNpc(
            SceneData sceneData,
            string npcId,
            string offerId,
            int day,
            int minimumStock,
            int maximumStock)
        {
            if (sceneData == null)
                throw new ArgumentNullException(nameof(sceneData));
            if (string.IsNullOrWhiteSpace(npcId))
                throw new ArgumentException(
                    "NPC id cannot be empty.",
                    nameof(npcId));

            string scopedOfferId = BuildNpcOfferId(npcId, offerId);
            sceneData.MarketStock ??= new();
            MarketStockData scoped = sceneData.MarketStock.FirstOrDefault(
                item => item != null && item.OfferId == scopedOfferId);
            if (scoped != null)
            {
                return GetOrRefresh(
                    sceneData,
                    scopedOfferId,
                    day,
                    minimumStock,
                    maximumStock);
            }

            MarketStockData legacy = sceneData.MarketStock.FirstOrDefault(
                item => item != null &&
                    item.OfferId == offerId &&
                    item.Day == Math.Max(1, day));
            if (legacy != null)
            {
                scoped = new MarketStockData(
                    scopedOfferId,
                    legacy.Day,
                    legacy.Remaining);
                sceneData.MarketStock.Add(scoped);
                return scoped;
            }

            return GetOrRefresh(
                sceneData,
                scopedOfferId,
                day,
                minimumStock,
                maximumStock);
        }

        public static bool TryConsumeNpc(
            SceneData sceneData,
            string npcId,
            string offerId,
            int day,
            int minimumStock,
            int maximumStock)
        {
            MarketStockData state = GetOrRefreshNpc(
                sceneData,
                npcId,
                offerId,
                day,
                minimumStock,
                maximumStock);
            if (state.Remaining <= 0)
                return false;

            state.Remaining--;
            return true;
        }

        public static void RestoreNpc(
            SceneData sceneData,
            string npcId,
            string offerId,
            int day,
            int minimumStock,
            int maximumStock)
        {
            GetOrRefreshNpc(
                sceneData,
                npcId,
                offerId,
                day,
                minimumStock,
                maximumStock).Remaining++;
        }

        public static MarketStockData GetOrRefresh(
            SceneData sceneData,
            string offerId,
            int day,
            int minimumStock,
            int maximumStock)
        {
            if (sceneData == null)
                throw new ArgumentNullException(nameof(sceneData));
            if (string.IsNullOrWhiteSpace(offerId))
                throw new ArgumentException(
                    "Offer id cannot be empty.",
                    nameof(offerId));

            sceneData.MarketStock ??= new();
            MarketStockData state = sceneData.MarketStock.FirstOrDefault(
                item => item != null && item.OfferId == offerId);
            if (state == null)
            {
                state = new MarketStockData();
                state.OfferId = offerId;
                sceneData.MarketStock.Add(state);
            }

            int normalizedDay = Math.Max(1, day);
            int normalizedMinimum = Math.Max(1, minimumStock);
            int normalizedMaximum = Math.Max(
                normalizedMinimum,
                maximumStock);
            if (state.Day != normalizedDay)
            {
                state.Day = normalizedDay;
                state.Remaining = CalculateDailyStock(
                    offerId,
                    normalizedDay,
                    normalizedMinimum,
                    normalizedMaximum);
            }

            return state;
        }

        public static bool TryConsume(
            SceneData sceneData,
            string offerId,
            int day,
            int minimumStock,
            int maximumStock)
        {
            MarketStockData state = GetOrRefresh(
                sceneData,
                offerId,
                day,
                minimumStock,
                maximumStock);
            if (state.Remaining <= 0)
                return false;

            state.Remaining--;
            return true;
        }

        public static void RestoreOne(
            SceneData sceneData,
            string offerId,
            int day,
            int minimumStock,
            int maximumStock)
        {
            MarketStockData state = GetOrRefresh(
                sceneData,
                offerId,
                day,
                minimumStock,
                maximumStock);
            state.Remaining++;
        }

        private static int CalculateDailyStock(
            string offerId,
            int day,
            int minimumStock,
            int maximumStock)
        {
            uint hash = 2166136261;
            foreach (char character in offerId)
            {
                hash ^= character;
                hash *= 16777619;
            }

            hash ^= (uint)day;
            hash *= 16777619;
            int range = maximumStock - minimumStock + 1;
            return minimumStock + (int)(hash % (uint)range);
        }

        private static string BuildNpcOfferId(
            string npcId,
            string offerId)
        {
            return $"npc:{npcId}:{offerId}";
        }
    }
}
