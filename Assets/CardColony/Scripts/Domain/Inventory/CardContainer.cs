using System;
using System.Collections.Generic;
using System.Linq;

namespace CardColony.Inventory
{
    /// <summary>
    /// Slot and weight constrained container whose contents are always represented by item cards.
    /// </summary>
    [Serializable]
    public sealed class CardContainer
    {
        private readonly List<ItemCardStack> cards = new List<ItemCardStack>();

        public int SlotCapacity { get; }
        public float MaxWeight { get; }
        public IReadOnlyList<ItemCardStack> Cards => cards;
        public float CurrentWeight => cards.Sum(card => card.TotalWeight);

        public CardContainer(int slotCapacity, float maxWeight)
        {
            if (slotCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotCapacity));
            if (maxWeight <= 0f || float.IsNaN(maxWeight) || float.IsInfinity(maxWeight))
                throw new ArgumentOutOfRangeException(nameof(maxWeight));

            SlotCapacity = slotCapacity;
            MaxWeight = maxWeight;
        }

        public InventoryAddResult Add(ItemCardStack incoming)
        {
            if (incoming == null)
                throw new ArgumentNullException(nameof(incoming));

            int remaining = incoming.Quantity;
            int accepted = 0;

            foreach (ItemCardStack existing in cards.Where(card => card.CanMergeWith(incoming)))
            {
                int amount = GetFittingQuantity(incoming, remaining, existing.FreeStackSpace);
                if (amount <= 0)
                    continue;

                existing.Increase(amount);
                remaining -= amount;
                accepted += amount;

                if (remaining == 0)
                    return new InventoryAddResult(accepted, 0);
            }

            bool preserveIncomingId = !cards.Any(card => card.InstanceId == incoming.InstanceId);
            while (remaining > 0 && cards.Count < SlotCapacity)
            {
                int amount = GetFittingQuantity(incoming, remaining, incoming.MaxStackSize);
                if (amount <= 0)
                    break;

                ItemCardStack newCard = incoming.CopyWithQuantity(amount, preserveIncomingId);
                cards.Add(newCard);
                preserveIncomingId = false;
                remaining -= amount;
                accepted += amount;
            }

            return new InventoryAddResult(accepted, remaining);
        }

        /// <summary>
        /// Adds the complete card stack or leaves the container unchanged.
        /// </summary>
        public bool TryAddAll(ItemCardStack incoming)
        {
            if (incoming == null)
                throw new ArgumentNullException(nameof(incoming));

            if (!CanAddAll(incoming))
                return false;

            InventoryAddResult result = Add(incoming);
            if (!result.IsComplete)
                throw new InvalidOperationException("Container capacity changed during a transactional add.");

            return true;
        }

        public bool CanAddAll(ItemCardStack incoming)
        {
            if (incoming == null)
                throw new ArgumentNullException(nameof(incoming));

            CardContainer simulation = FromSnapshot(CreateSnapshot());
            return simulation.Add(incoming).IsComplete;
        }

        public bool TryRemove(string cardInstanceId, int quantity, out ItemCardStack detachedCard)
        {
            if (string.IsNullOrWhiteSpace(cardInstanceId))
                throw new ArgumentException("Card instance ID cannot be empty.", nameof(cardInstanceId));
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));

            ItemCardStack source = cards.FirstOrDefault(card => card.InstanceId == cardInstanceId);
            if (source == null || quantity > source.Quantity)
            {
                detachedCard = null;
                return false;
            }

            detachedCard = source.CopyWithQuantity(quantity, false);
            source.Decrease(quantity);
            if (source.Quantity == 0)
                cards.Remove(source);

            return true;
        }

        public int GetQuantity(string itemId)
        {
            ValidateItemId(itemId);

            int total = 0;
            foreach (ItemCardStack card in cards)
            {
                if (card.ItemId == itemId)
                    total = checked(total + card.Quantity);
            }

            return total;
        }

        public bool TryConsume(string itemId, int quantity)
        {
            ValidateItemId(itemId);
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            if (GetQuantity(itemId) < quantity)
                return false;

            int remaining = quantity;
            for (int index = cards.Count - 1; index >= 0 && remaining > 0; index--)
            {
                ItemCardStack card = cards[index];
                if (card.ItemId != itemId)
                    continue;

                int amount = Math.Min(card.Quantity, remaining);
                card.Decrease(amount);
                remaining -= amount;
                if (card.Quantity == 0)
                    cards.RemoveAt(index);
            }

            return true;
        }

        public CardContainerSnapshot CreateSnapshot()
        {
            return new CardContainerSnapshot
            {
                SlotCapacity = SlotCapacity,
                MaxWeight = MaxWeight,
                Cards = cards.Select(card => new ItemCardSnapshot
                {
                    ItemId = card.ItemId,
                    Quantity = card.Quantity,
                    MaxStackSize = card.MaxStackSize,
                    UnitWeight = card.UnitWeight,
                    Quality = card.Quality,
                    BatchId = card.BatchId,
                    InstanceId = card.InstanceId
                }).ToList()
            };
        }

        public static CardContainer FromSnapshot(CardContainerSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var container = new CardContainer(snapshot.SlotCapacity, snapshot.MaxWeight);
            var instanceIds = new HashSet<string>();
            foreach (ItemCardSnapshot card in snapshot.Cards ?? new List<ItemCardSnapshot>())
            {
                if (card == null)
                    throw new ArgumentException("Container snapshot contains a null card.", nameof(snapshot));
                if (!instanceIds.Add(card.InstanceId))
                    throw new ArgumentException($"Duplicate card instance ID '{card.InstanceId}'.", nameof(snapshot));

                var itemCard = new ItemCardStack(
                    card.ItemId,
                    card.Quantity,
                    card.MaxStackSize,
                    card.UnitWeight,
                    card.Quality,
                    card.BatchId,
                    card.InstanceId);
                InventoryAddResult result = container.Add(itemCard);
                if (!result.IsComplete)
                    throw new ArgumentException("Container snapshot exceeds its slot or weight capacity.", nameof(snapshot));
            }

            return container;
        }

        private int GetFittingQuantity(ItemCardStack incoming, int requested, int stackSpace)
        {
            int byStack = Math.Min(requested, stackSpace);
            if (incoming.UnitWeight == 0f)
                return byStack;

            double availableWeight = Math.Max(0d, (double)MaxWeight - CurrentWeight);
            double requestedWeight = (double)byStack * incoming.UnitWeight;
            if (requestedWeight <= availableWeight)
                return byStack;

            int byWeight = (int)Math.Floor(availableWeight / incoming.UnitWeight);
            return Math.Min(byStack, byWeight);
        }

        private static void ValidateItemId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("Item ID cannot be empty.", nameof(itemId));
        }
    }
}
