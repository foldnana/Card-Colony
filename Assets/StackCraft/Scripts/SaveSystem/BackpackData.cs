using System;
using System.Collections.Generic;

namespace CryingSnow.StackCraft
{
    [Serializable]
    public sealed class BackpackData
    {
        public int SlotCapacity = 8;
        public List<BackpackEntryData> Entries = new();

        public int Capacity => SlotCapacity;
        public int Count => Entries?.Count ?? 0;

        public void Normalize()
        {
            if (SlotCapacity <= 0)
                SlotCapacity = 8;
            Entries ??= new List<BackpackEntryData>();

            var usedIds = new HashSet<string>();
            var usedSlots = new HashSet<int>();
            Entries.RemoveAll(entry => entry?.Card == null);
            EnsureCapacity(Entries.Count);
            foreach (BackpackEntryData entry in Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.InstanceId) ||
                    !usedIds.Add(entry.InstanceId))
                {
                    entry.InstanceId = Guid.NewGuid().ToString("N");
                    usedIds.Add(entry.InstanceId);
                }

                if (entry.SlotIndex < 0 || entry.SlotIndex >= SlotCapacity ||
                    !usedSlots.Add(entry.SlotIndex))
                {
                    entry.SlotIndex = FindFirstFreeSlot(usedSlots);
                    usedSlots.Add(entry.SlotIndex);
                }
            }
        }

        public bool TryAdd(CardData card, out BackpackEntryData entry)
        {
            entry = null;
            if (card == null)
                return false;

            Normalize();
            if (Entries.Count >= SlotCapacity)
                EnsureCapacity(Entries.Count + 1);

            int slotIndex = FindFirstFreeSlot();
            entry = new BackpackEntryData
            {
                InstanceId = Guid.NewGuid().ToString("N"),
                Card = card,
                SlotIndex = slotIndex
            };
            Entries.Add(entry);
            return true;
        }

        public bool TryRemove(string instanceId, out CardData card)
        {
            card = null;
            if (string.IsNullOrWhiteSpace(instanceId) || Entries == null)
                return false;

            int index = Entries.FindIndex(entry =>
                entry != null && entry.InstanceId == instanceId);
            if (index < 0)
                return false;

            card = Entries[index].Card;
            Entries.RemoveAt(index);
            return card != null;
        }

        public BackpackEntryData Find(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || Entries == null)
                return null;

            return Entries.Find(entry => entry != null && entry.InstanceId == instanceId);
        }

        public void Compact()
        {
            Normalize();
            Entries.Sort((left, right) => left.SlotIndex.CompareTo(right.SlotIndex));
            for (int index = 0; index < Entries.Count; index++)
                Entries[index].SlotIndex = index;
        }

        private int FindFirstFreeSlot()
        {
            for (int slotIndex = 0; slotIndex < SlotCapacity; slotIndex++)
            {
                if (!Entries.Exists(entry => entry != null && entry.SlotIndex == slotIndex))
                    return slotIndex;
            }

            return Entries.Count;
        }

        private int FindFirstFreeSlot(HashSet<int> usedSlots)
        {
            for (int slotIndex = 0; slotIndex < SlotCapacity; slotIndex++)
            {
                if (!usedSlots.Contains(slotIndex))
                    return slotIndex;
            }

            return usedSlots.Count;
        }

        private void EnsureCapacity(int requiredSlots)
        {
            if (requiredSlots <= SlotCapacity)
                return;

            const int slotsPerRow = 4;
            SlotCapacity = ((requiredSlots + slotsPerRow - 1) / slotsPerRow) *
                slotsPerRow;
        }
    }

    [Serializable]
    public sealed class BackpackEntryData
    {
        public string InstanceId;
        public CardData Card;
        public int SlotIndex;
    }
}
