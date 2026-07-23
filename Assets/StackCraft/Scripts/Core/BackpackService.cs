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
            return TryStoreInternal(
                card,
                backpack,
                hasTablePosition: false,
                default,
                null,
                0);
        }

        public static bool TryStoreAtTablePosition(
            CardInstance card,
            BackpackData backpack,
            UnityEngine.Vector2 tablePosition)
        {
            return TryStoreInternal(
                card,
                backpack,
                hasTablePosition: true,
                tablePosition,
                null,
                0);
        }

        public static bool TryStoreAtTablePosition(
            CardInstance card,
            BackpackData backpack,
            UnityEngine.Vector2 tablePosition,
            string tableStackId,
            int tableStackOrder)
        {
            return TryStoreInternal(
                card,
                backpack,
                hasTablePosition: true,
                tablePosition,
                tableStackId,
                tableStackOrder);
        }

        private static bool TryStoreInternal(
            CardInstance card,
            BackpackData backpack,
            bool hasTablePosition,
            UnityEngine.Vector2 tablePosition,
            string requestedTableStackId,
            int requestedTableStackOrder)
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
            string tableStackId =
                string.IsNullOrWhiteSpace(requestedTableStackId)
                    ? System.Guid.NewGuid().ToString("N")
                    : requestedTableStackId;
            int tableStackOrder = System.Math.Max(
                0,
                requestedTableStackOrder);
            foreach (CardInstance candidate in cards)
            {
                if (!backpack.TryAdd(new CardData(candidate), out BackpackEntryData entry))
                {
                    foreach (string entryId in addedEntryIds)
                        backpack.TryRemove(entryId, out _);
                    return false;
                }

                entry.TableStackId = tableStackId;
                entry.TableStackOrder = tableStackOrder++;
                if (hasTablePosition)
                {
                    entry.HasTablePosition = true;
                    entry.TablePositionX = tablePosition.x;
                    entry.TablePositionZ = tablePosition.y;
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

        public static bool TryTakeExisting(
            BackpackData backpack,
            string instanceId,
            System.Func<bool> transfer)
        {
            if (backpack?.Find(instanceId)?.Card == null || transfer == null)
                return false;

            if (!transfer.Invoke())
                return false;

            if (!backpack.TryRemove(instanceId, out _))
                return false;

            CardManager.Instance?.NotifyStatsChanged();
            Changed?.Invoke();
            return true;
        }

        public static bool TryTakeExistingStack(
            BackpackData backpack,
            System.Collections.Generic.IReadOnlyCollection<string> instanceIds,
            System.Func<bool> transfer)
        {
            return TryTakeExistingStack(
                backpack,
                instanceIds,
                transfer,
                rollback: null);
        }

        public static bool TryTakeExistingStack(
            BackpackData backpack,
            System.Collections.Generic.IReadOnlyCollection<string> instanceIds,
            System.Func<bool> transfer,
            System.Action rollback)
        {
            if (backpack == null || instanceIds == null ||
                instanceIds.Count == 0 || transfer == null)
                return false;

            List<string> uniqueIds = instanceIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();
            if (uniqueIds.Count != instanceIds.Count ||
                uniqueIds.Any(id => backpack.Find(id)?.Card == null))
                return false;

            List<BackpackEntryData> removedEntries = uniqueIds
                .Select(backpack.Find)
                .ToList();
            foreach (string instanceId in uniqueIds)
            {
                if (!backpack.TryRemove(instanceId, out _))
                {
                    RestoreEntries(backpack, removedEntries);
                    return false;
                }
            }

            bool transferred = false;
            try
            {
                transferred = transfer.Invoke();
            }
            finally
            {
                if (!transferred)
                {
                    RestoreEntries(backpack, removedEntries);
                    rollback?.Invoke();
                }
            }

            if (!transferred)
                return false;

            CardManager.Instance?.NotifyStatsChanged();
            Changed?.Invoke();
            return true;
        }

        private static void RestoreEntries(
            BackpackData backpack,
            IEnumerable<BackpackEntryData> entries)
        {
            foreach (BackpackEntryData entry in entries)
            {
                if (entry != null && backpack.Find(entry.InstanceId) == null)
                    backpack.Entries.Add(entry);
            }

            backpack.Normalize();
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
