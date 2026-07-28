using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CreateAssetMenu(
        menuName = "StackCraft/Trading/NPC Trade Role Template",
        fileName = "NpcTradeRole_")]
    public sealed class NpcTradeRoleTemplate : ScriptableObject
    {
        [SerializeField] private string roleId;
        [SerializeField] private string roleLabel = "居民";
        [SerializeField, Min(0)] private int startingFunds = 5;
        [SerializeField] private List<CardCategory> buyCategories = new();

        public string RoleId => roleId;
        public string RoleLabel => roleLabel;
        public int StartingFunds => startingFunds;
        public IReadOnlyList<CardCategory> BuyCategories => buyCategories;
    }

    [CreateAssetMenu(
        menuName = "StackCraft/Trading/NPC Trade Profile",
        fileName = "NpcTradeProfile_")]
    public sealed class NpcTradeProfile : ScriptableObject
    {
        [SerializeField] private CardDefinition npcDefinition;
        [SerializeField] private NpcTradeRoleTemplate roleTemplate;
        [SerializeField] private string roleLabelOverride;
        [SerializeField] private int startingFundsOverride = -1;
        [SerializeField] private List<CardCategory> buyCategoriesOverride = new();
        [SerializeField] private List<CardDefinition> rejectedDefinitions = new();
        [SerializeField, Min(0.01f)] private float buyPriceModifier = 1f;
        [SerializeField, Min(0.01f)] private float sellPriceModifier = 1f;
        [SerializeField] private string refusalText = "这个人物不收购该物品。";
        [Header("Regional market")]
        [SerializeField] private MarketProfile marketProfile;
        [SerializeField] private List<string> sellCommodityTags = new();
        [SerializeField] private List<string> buyCommodityTags = new();
        [SerializeField] private bool usesMarketFunds = true;
        [SerializeField, Min(0)] private int personalFundLimit;

        public CardDefinition NpcDefinition => npcDefinition;
        public NpcTradeRoleTemplate RoleTemplate => roleTemplate;
        public string RoleLabelOverride => roleLabelOverride;
        public int StartingFundsOverride => startingFundsOverride;
        public IReadOnlyList<CardCategory> BuyCategoriesOverride =>
            buyCategoriesOverride;
        public IReadOnlyList<CardDefinition> RejectedDefinitions =>
            rejectedDefinitions;
        public float BuyPriceModifier => buyPriceModifier;
        public float SellPriceModifier => sellPriceModifier;
        public string RefusalText => refusalText;
        public MarketProfile MarketProfile => marketProfile;
        public IReadOnlyList<string> SellCommodityTags =>
            sellCommodityTags;
        public IReadOnlyList<string> BuyCommodityTags =>
            buyCommodityTags;
        public bool UsesMarketFunds => usesMarketFunds;
        public int PersonalFundLimit => Mathf.Max(0, personalFundLimit);
    }
}
