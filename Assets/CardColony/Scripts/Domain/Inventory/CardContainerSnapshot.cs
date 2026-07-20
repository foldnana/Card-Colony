using System;
using System.Collections.Generic;

namespace CardColony.Inventory
{
    [Serializable]
    public sealed class CardContainerSnapshot
    {
        public int SlotCapacity;
        public float MaxWeight;
        public List<ItemCardSnapshot> Cards = new List<ItemCardSnapshot>();
    }

    [Serializable]
    public sealed class ItemCardSnapshot
    {
        public string ItemId;
        public int Quantity;
        public int MaxStackSize;
        public float UnitWeight;
        public int Quality;
        public string BatchId;
        public string InstanceId;
    }
}
