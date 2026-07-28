using System;
using System.Collections.Generic;

namespace CryingSnow.StackCraft
{
    [Serializable]
    public sealed class MarketStateData
    {
        public string MarketId;
        public int AvailableFunds;
        public long LastRefreshWorldHour;
        public long NextRefreshWorldHour;
        public int RefreshSequence;
        public int StateRevision;
        public int StateVersion;
        public List<MarketCommodityStateData> Commodities = new();
        public List<string> ActiveEventIds = new();

        public MarketCommodityStateData GetCommodity(string commodityId)
        {
            Commodities ??= new List<MarketCommodityStateData>();
            return Commodities.Find(item =>
                item != null && item.CommodityId == commodityId);
        }
    }

    [Serializable]
    public sealed class MarketCommodityStateData
    {
        public string CommodityId;
        public int Stock;
        public int RecentPlayerPurchaseQuantity;
        public int RecentPlayerSaleQuantity;
        public long LastTradeWorldHour;
    }
}
