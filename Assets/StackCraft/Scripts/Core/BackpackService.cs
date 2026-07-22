using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public static class BackpackService
    {
        public static event System.Action Changed;

        public static BackpackData Current =>
            GameDirector.Instance?.GameData?.EnsureBackpack();

        public static bool CanStoreDefinition(CardDefinition definition)
        {
            if (definition == null || definition.IsLocationStatic)
                return false;

            return definition.Category is
                CardCategory.Consumable or
                CardCategory.Material or
                CardCategory.Equipment or
                CardCategory.Currency or
                CardCategory.Valuable;
        }

        public static bool TryStore(CardInstance card)
        {
            return TryStore(card, Current);
        }

        public static bool TryStore(CardInstance card, BackpackData backpack)
        {
            if (card == null || backpack == null || card.Stack == null ||
                card.Stack.IsCrafting)
                return false;

            CardStack sourceStack = card.Stack;
            List<CardInstance> cards = sourceStack.Cards.ToList();
            if (cards.Count == 0 || cards.Any(candidate => candidate == null ||
                    !CanStoreDefinition(candidate.Definition) ||
                    (candidate.Combatant != null && candidate.Combatant.IsInCombat)))
                return false;

            var addedEntryIds = new List<string>();
            foreach (CardInstance candidate in cards)
            {
                if (!backpack.TryAdd(new CardData(candidate), out BackpackEntryData entry))
                {
                    foreach (string entryId in addedEntryIds)
                        backpack.TryRemove(entryId, out _);
                    return false;
                }

                addedEntryIds.Add(entry.InstanceId);
            }

            foreach (CardInstance candidate in cards)
                sourceStack.DestroyCard(candidate);

            CardManager.Instance?.NotifyStatsChanged();
            Changed?.Invoke();
            return true;
        }

        public static bool TryTake(
            string instanceId,
            UnityEngine.Vector3 worldPosition,
            out CardInstance worldCard)
        {
            worldCard = null;
            if (Current == null || CardManager.Instance == null)
                return false;

            return TryTake(
                Current,
                instanceId,
                data => CardManager.Instance.RestoreCardFromData(
                    data,
                    worldPosition,
                    notifyStats: false),
                out worldCard);
        }

        public static bool TryTake(
            BackpackData backpack,
            string instanceId,
            System.Func<CardData, CardInstance> restore,
            out CardInstance worldCard)
        {
            worldCard = null;
            if (backpack == null || restore == null)
                return false;

            BackpackEntryData entry = backpack.Find(instanceId);
            if (entry?.Card == null)
                return false;

            CardInstance restored = restore(entry.Card);
            if (restored == null)
                return false;

            if (!backpack.TryRemove(instanceId, out _))
            {
                restored.Stack?.DestroyCard(restored);
                return false;
            }

            worldCard = restored;
            CardManager.Instance?.NotifyStatsChanged();
            Changed?.Invoke();
            return true;
        }

        public static void Arrange()
        {
            BackpackData backpack = Current;
            if (backpack == null)
                return;

            backpack.Compact();
            Changed?.Invoke();
        }
    }
}
