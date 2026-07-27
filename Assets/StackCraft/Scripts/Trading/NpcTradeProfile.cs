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
    }
}
