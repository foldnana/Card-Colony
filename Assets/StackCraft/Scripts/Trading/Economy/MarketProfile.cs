using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [Serializable]
    public sealed class MarketCommodityRule
    {
        [SerializeField] private CommodityDefinition commodity;
        [SerializeField, Min(0.01f)] private float regionalPriceFactor = 1f;
        [SerializeField, Min(0)] private int initialStockMin;
        [SerializeField, Min(0)] private int initialStockMax;
        [SerializeField, Min(1)] private int targetStock = 10;
        [SerializeField, Min(0)] private int productionPerRefresh;
        [SerializeField, Min(0)] private int consumptionPerRefresh;
        [SerializeField, Min(0)] private int minimumStock;
        [SerializeField, Min(0)] private int maximumStock = 99;
        [SerializeField] private bool allowsPlayerPurchase = true;
        [SerializeField] private bool allowsPlayerSale = true;

        public CommodityDefinition Commodity => commodity;
        public float RegionalPriceFactor =>
            Mathf.Max(0.01f, regionalPriceFactor);
        public int InitialStockMin => Mathf.Max(0, initialStockMin);
        public int InitialStockMax =>
            Mathf.Max(InitialStockMin, initialStockMax);
        public int TargetStock => Mathf.Max(1, targetStock);
        public int ProductionPerRefresh =>
            Mathf.Max(0, productionPerRefresh);
        public int ConsumptionPerRefresh =>
            Mathf.Max(0, consumptionPerRefresh);
        public int MinimumStock => Mathf.Max(0, minimumStock);
        public int MaximumStock => Mathf.Max(MinimumStock, maximumStock);
        public bool AllowsPlayerPurchase => allowsPlayerPurchase;
        public bool AllowsPlayerSale => allowsPlayerSale;
    }

    [CreateAssetMenu(
        menuName = "StackCraft/Trading/Market Profile",
        fileName = "Market_")]
    public sealed class MarketProfile : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string locationId;
        [SerializeField] private CardDefinition currency;
        [SerializeField, Range(0.01f, 0.5f)] private float baseSpread = 0.18f;
        [SerializeField, Min(0)] private int startingFunds;
        [SerializeField, Min(0)] private int maximumFunds;
        [SerializeField, Min(0)] private int fundRecovery;
        [SerializeField, Min(1)] private int refreshHoursMin = 48;
        [SerializeField, Min(1)] private int refreshHoursMax = 96;
        [SerializeField, Range(0f, 0.5f)] private float transactionFee;
        [SerializeField] private List<MarketCommodityRule> commodityRules =
            new();

        public string Id => id;
        public string LocationId => locationId;
        public CardDefinition Currency => currency;
        public float BaseSpread => Mathf.Clamp(baseSpread, 0.01f, 0.5f);
        public int StartingFunds => Mathf.Max(0, startingFunds);
        public int MaximumFunds => Mathf.Max(StartingFunds, maximumFunds);
        public int FundRecovery => Mathf.Max(0, fundRecovery);
        public int RefreshHoursMin => Mathf.Max(1, refreshHoursMin);
        public int RefreshHoursMax =>
            Mathf.Max(RefreshHoursMin, refreshHoursMax);
        public float TransactionFee =>
            Mathf.Clamp(transactionFee, 0f, 0.5f);
        public IReadOnlyList<MarketCommodityRule> CommodityRules =>
            commodityRules;

        public MarketCommodityRule GetRule(string commodityId)
        {
            return commodityRules?.FirstOrDefault(rule =>
                rule?.Commodity != null &&
                rule.Commodity.Id == commodityId);
        }
    }
}
