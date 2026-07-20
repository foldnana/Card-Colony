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
    }
}
