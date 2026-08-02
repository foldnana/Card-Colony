using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public static class MarketCurrencyService
    {
        public static int CountAvailable(
            CardDefinition currency,
            BackpackData backpack,
            IEnumerable<CardInstance> worldCards)
        {
            if (currency == null)
                return 0;

            int backpackCount = backpack?.Entries?.Count(entry =>
                entry?.IsAvailable == true && entry.Card.Id == currency.Id) ?? 0;
            int worldCount = worldCards?.Count(card =>
                card != null && card.BaseDefinition == currency) ?? 0;
            return backpackCount + worldCount;
        }

        public static bool TrySpend(
            CardDefinition currency,
            int amount,
            BackpackData backpack,
            IEnumerable<CardInstance> worldCards)
        {
            if (currency == null || amount <= 0)
                return false;

            List<BackpackEntryData> storedCoins = backpack?.Entries?
                .Where(entry =>
                    entry?.IsAvailable == true &&
                    entry.Card.Id == currency.Id)
                .Take(amount)
                .ToList() ?? new List<BackpackEntryData>();
            int remaining = amount - storedCoins.Count;
            List<CardInstance> tableCoins = worldCards?
                .Where(card =>
                    card != null &&
                    card.Stack != null &&
                    card.BaseDefinition == currency)
                .Take(remaining)
                .ToList() ?? new List<CardInstance>();
            if (storedCoins.Count + tableCoins.Count < amount)
                return false;

            var removedEntries = new List<BackpackEntryData>();
            foreach (BackpackEntryData entry in storedCoins)
            {
                if (!backpack.TryRemove(entry.InstanceId, out _))
                {
                    RestoreEntries(backpack, removedEntries);
                    return false;
                }
                removedEntries.Add(entry);
            }

            foreach (CardInstance coin in tableCoins)
                coin.Stack?.DestroyCard(coin);

            BackpackService.NotifyContentsChanged();
            return true;
        }

        private static void RestoreEntries(
            BackpackData backpack,
            IEnumerable<BackpackEntryData> entries)
        {
            if (backpack == null)
                return;

            foreach (BackpackEntryData entry in entries)
            {
                if (entry != null)
                    backpack.Entries.Add(entry);
            }
            backpack.Normalize();
        }
    }
}
