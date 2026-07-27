using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public sealed class ResolvedNpcTradeProfile
    {
        private readonly HashSet<CardCategory> buyCategories;
        private readonly HashSet<CardDefinition> rejectedDefinitions;

        public ResolvedNpcTradeProfile(
            string roleLabel,
            int startingFunds,
            IEnumerable<CardCategory> buyCategories)
            : this(
                roleLabel,
                startingFunds,
                buyCategories,
                null,
                1f,
                1f,
                null)
        {
        }

        public ResolvedNpcTradeProfile(
            string roleLabel,
            int startingFunds,
            IEnumerable<CardCategory> buyCategories,
            IEnumerable<CardDefinition> rejectedDefinitions,
            float buyPriceModifier,
            float sellPriceModifier,
            string refusalText)
        {
            RoleLabel = string.IsNullOrWhiteSpace(roleLabel)
                ? "居民"
                : roleLabel;
            StartingFunds = System.Math.Max(0, startingFunds);
            this.buyCategories = buyCategories == null
                ? new HashSet<CardCategory>()
                : new HashSet<CardCategory>(buyCategories);
            this.rejectedDefinitions = rejectedDefinitions == null
                ? new HashSet<CardDefinition>()
                : new HashSet<CardDefinition>(
                    rejectedDefinitions.Where(item => item != null));
            BuyPriceModifier = Mathf.Max(0.01f, buyPriceModifier);
            SellPriceModifier = Mathf.Max(0.01f, sellPriceModifier);
            RefusalText = string.IsNullOrWhiteSpace(refusalText)
                ? "这个人物不收购该物品。"
                : refusalText;
        }

        public string RoleLabel { get; }
        public int StartingFunds { get; }
        public float BuyPriceModifier { get; }
        public float SellPriceModifier { get; }
        public string RefusalText { get; }
        public IReadOnlyCollection<CardCategory> BuyCategories =>
            buyCategories;

        public bool CanBuyCategory(CardCategory category)
        {
            return buyCategories.Contains(category);
        }

        public bool CanBuyDefinition(CardDefinition definition)
        {
            if (definition == null || !CanBuyCategory(definition.Category))
                return false;

            return !rejectedDefinitions.Any(rejected =>
                rejected == definition ||
                (!string.IsNullOrWhiteSpace(rejected.Id) &&
                 rejected.Id == definition.Id));
        }

        public int CalculatePlayerBuyPrice(int configuredPrice)
        {
            return CalculatePrice(configuredPrice, SellPriceModifier);
        }

        public int CalculatePlayerSellPrice(int baseSellPrice)
        {
            return CalculatePrice(baseSellPrice, BuyPriceModifier);
        }

        private static int CalculatePrice(int basePrice, float modifier)
        {
            return basePrice <= 0
                ? 0
                : Mathf.Max(1, Mathf.RoundToInt(basePrice * modifier));
        }
    }

    public static class NpcTradeProfileResolver
    {
        public static ResolvedNpcTradeProfile Resolve(
            CardDefinition definition)
        {
            return Resolve(definition, null);
        }

        public static ResolvedNpcTradeProfile Resolve(
            CardDefinition definition,
            NpcTradeProfile explicitProfile)
        {
            if (explicitProfile != null)
            {
                NpcTradeRoleTemplate role = explicitProfile.RoleTemplate;
                string label = string.IsNullOrWhiteSpace(
                    explicitProfile.RoleLabelOverride)
                    ? role?.RoleLabel
                    : explicitProfile.RoleLabelOverride;
                int funds = explicitProfile.StartingFundsOverride >= 0
                    ? explicitProfile.StartingFundsOverride
                    : role?.StartingFunds ?? 5;
                IEnumerable<CardCategory> categories =
                    explicitProfile.BuyCategoriesOverride?.Count > 0
                        ? explicitProfile.BuyCategoriesOverride
                        : role?.BuyCategories;
                return new ResolvedNpcTradeProfile(
                    label,
                    funds,
                    categories,
                    explicitProfile.RejectedDefinitions,
                    explicitProfile.BuyPriceModifier,
                    explicitProfile.SellPriceModifier,
                    explicitProfile.RefusalText);
            }

            string id = definition?.Id?.ToLowerInvariant() ?? string.Empty;
            if (id.Contains("blacksmith"))
            {
                return new ResolvedNpcTradeProfile(
                    "铁匠",
                    20,
                    new[]
                    {
                        CardCategory.Material,
                        CardCategory.Equipment
                    });
            }

            if (id.Contains("apothecary"))
            {
                return new ResolvedNpcTradeProfile(
                    "药师",
                    15,
                    new[] { CardCategory.Consumable });
            }

            if (id.Contains("innkeeper"))
            {
                return new ResolvedNpcTradeProfile(
                    "旅店老板",
                    12,
                    new[]
                    {
                        CardCategory.Consumable,
                        CardCategory.Material
                    });
            }

            if (id.Contains("chief"))
            {
                return new ResolvedNpcTradeProfile(
                    "村长",
                    10,
                    new[] { CardCategory.Valuable });
            }

            if (id.Contains("grocer"))
            {
                return new ResolvedNpcTradeProfile(
                    "杂货商",
                    30,
                    new[]
                    {
                        CardCategory.Consumable,
                        CardCategory.Material,
                        CardCategory.Valuable
                    });
            }

            return new ResolvedNpcTradeProfile(
                "居民",
                5,
                new[]
                {
                    CardCategory.Consumable,
                    CardCategory.Valuable
                });
        }
    }
}
