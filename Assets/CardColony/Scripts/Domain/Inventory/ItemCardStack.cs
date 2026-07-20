using System;

namespace CardColony.Inventory
{
    /// <summary>
    /// One visible item card inside a container. Quantity is a property of the card, not a hidden list entry.
    /// </summary>
    [Serializable]
    public sealed class ItemCardStack
    {
        public string ItemId { get; }
        public int Quantity { get; private set; }
        public int MaxStackSize { get; }
        public float UnitWeight { get; }
        public int Quality { get; }
        public string BatchId { get; }
        public string InstanceId { get; }

        public float TotalWeight => Quantity * UnitWeight;
        public int FreeStackSpace => Math.Max(0, MaxStackSize - Quantity);

        public ItemCardStack(
            string itemId,
            int quantity,
            int maxStackSize,
            float unitWeight,
            int quality = 0,
            string batchId = "",
            string instanceId = null)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("Item ID cannot be empty.", nameof(itemId));
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity));
            if (maxStackSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxStackSize));
            if (unitWeight < 0f || float.IsNaN(unitWeight) || float.IsInfinity(unitWeight))
                throw new ArgumentOutOfRangeException(nameof(unitWeight));

            ItemId = itemId;
            Quantity = quantity;
            MaxStackSize = maxStackSize;
            UnitWeight = unitWeight;
            Quality = quality;
            BatchId = batchId ?? string.Empty;
            InstanceId = string.IsNullOrWhiteSpace(instanceId)
                ? Guid.NewGuid().ToString("N")
                : instanceId;
        }

        internal bool CanMergeWith(ItemCardStack other)
        {
            return other != null
                && ItemId == other.ItemId
                && Quality == other.Quality
                && BatchId == other.BatchId
                && MaxStackSize == other.MaxStackSize
                && UnitWeight.Equals(other.UnitWeight);
        }

        internal void Increase(int quantity)
        {
            if (quantity <= 0 || quantity > FreeStackSpace)
                throw new ArgumentOutOfRangeException(nameof(quantity));

            Quantity += quantity;
        }

        internal void Decrease(int quantity)
        {
            if (quantity <= 0 || quantity > Quantity)
                throw new ArgumentOutOfRangeException(nameof(quantity));

            Quantity -= quantity;
        }

        internal ItemCardStack CopyWithQuantity(int quantity, bool preserveInstanceId)
        {
            return new ItemCardStack(
                ItemId,
                quantity,
                MaxStackSize,
                UnitWeight,
                Quality,
                BatchId,
                preserveInstanceId ? InstanceId : null);
        }
    }
}
