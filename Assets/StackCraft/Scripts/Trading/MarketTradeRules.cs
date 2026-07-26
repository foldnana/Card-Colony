using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public static class MarketTradeRules
    {
        public static bool CanSell(CardDefinition definition)
        {
            if (definition == null ||
                definition.IsLocationStatic ||
                !definition.IsSellable ||
                definition.SellPrice <= 0)
            {
                return false;
            }

            return definition.Category is
                CardCategory.Consumable or
                CardCategory.Material or
                CardCategory.Equipment or
                CardCategory.Valuable;
        }

        public static int CalculateSellValue(
            IEnumerable<CardInstance> cards)
        {
            return cards?
                .Where(card => card != null && CanSell(card.Definition))
                .Sum(card => card.Definition.SellPrice) ?? 0;
        }
    }
}
