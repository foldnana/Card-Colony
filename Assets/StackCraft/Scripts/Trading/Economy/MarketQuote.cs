namespace CryingSnow.StackCraft
{
    public enum MarketTrend
    {
        Abundant,
        Normal,
        Shortage,
        Emergency
    }

    public readonly struct MerchantPriceModifiers
    {
        private readonly float sellToPlayer;
        private readonly float buyFromPlayer;

        public MerchantPriceModifiers(
            float sellToPlayer,
            float buyFromPlayer)
        {
            this.sellToPlayer = sellToPlayer;
            this.buyFromPlayer = buyFromPlayer;
        }

        public float SellToPlayer =>
            sellToPlayer <= 0f ? 1f : sellToPlayer;
        public float BuyFromPlayer =>
            buyFromPlayer <= 0f ? 1f : buyFromPlayer;

        public static MerchantPriceModifiers Default =>
            new(1f, 1f);
    }

    public readonly struct MarketQuote
    {
        public MarketQuote(
            string marketId,
            string commodityId,
            int playerBuyUnitPrice,
            int playerSellUnitPrice,
            int availableStock,
            int marketAffordableQuantity,
            MarketTrend trend,
            long validAtWorldHour,
            int stateRevision)
        {
            MarketId = marketId;
            CommodityId = commodityId;
            PlayerBuyUnitPrice = playerBuyUnitPrice;
            PlayerSellUnitPrice = playerSellUnitPrice;
            AvailableStock = availableStock;
            MarketAffordableQuantity = marketAffordableQuantity;
            Trend = trend;
            ValidAtWorldHour = validAtWorldHour;
            StateRevision = stateRevision;
        }

        public string MarketId { get; }
        public string CommodityId { get; }
        public int PlayerBuyUnitPrice { get; }
        public int PlayerSellUnitPrice { get; }
        public int AvailableStock { get; }
        public int MarketAffordableQuantity { get; }
        public MarketTrend Trend { get; }
        public long ValidAtWorldHour { get; }
        public int StateRevision { get; }
    }
}
