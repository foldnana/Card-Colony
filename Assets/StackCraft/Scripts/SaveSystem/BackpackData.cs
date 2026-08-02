using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

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

            if (Entries[index].IsReserved)
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

        public bool TryMoveEntry(string instanceId, int targetSlotIndex)
        {
            Normalize();
            if (targetSlotIndex < 0 || targetSlotIndex >= SlotCapacity)
                return false;

            BackpackEntryData source = Find(instanceId);
            if (source == null || source.IsReserved)
                return false;
            if (source.SlotIndex == targetSlotIndex)
                return true;

            BackpackEntryData occupant = Entries.Find(entry =>
                entry != null && entry.SlotIndex == targetSlotIndex);
            if (occupant != null && occupant.IsReserved)
                return false;
            int sourceSlotIndex = source.SlotIndex;
            source.SlotIndex = targetSlotIndex;
            if (occupant != null)
                occupant.SlotIndex = sourceSlotIndex;
            return true;
        }

        public bool TrySetTablePlacement(
            string instanceId,
            float positionX,
            float positionZ,
            string stackId,
            int stackOrder)
        {
            BackpackEntryData entry = Find(instanceId);
            if (entry == null)
                return false;

            entry.HasTablePosition = true;
            entry.TablePositionX = positionX;
            entry.TablePositionZ = positionZ;
            entry.TableStackId = stackId;
            entry.TableStackOrder = Math.Max(0, stackOrder);
            return true;
        }

        public void Compact()
        {
            Normalize();
            var fixedSlots = new HashSet<int>(Entries
                .FindAll(entry => entry.IsReserved)
                .ConvertAll(entry => entry.SlotIndex));
            int nextSlot = 0;
            foreach (BackpackEntryData entry in Entries
                         .FindAll(entry => !entry.IsReserved))
            {
                while (fixedSlots.Contains(nextSlot))
                    nextSlot++;
                entry.SlotIndex = nextSlot++;
            }
            Entries.Sort((left, right) => left.SlotIndex.CompareTo(right.SlotIndex));
        }

        internal bool TryConsumeReserved(
            string instanceId,
            string commandId,
            out CardData card)
        {
            card = null;
            BackpackEntryData entry = Find(instanceId);
            if (entry == null || !entry.IsReserved ||
                entry.ReservationCommandId != commandId)
                return false;

            card = entry.Card;
            Entries.Remove(entry);
            return card != null;
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
        public bool HasTablePosition;
        public float TablePositionX;
        public float TablePositionZ;
        public string TableStackId;
        public int TableStackOrder;
        [FormerlySerializedAs("ReservedCombatSessionId")]
        public string ReservationOwnerId;
        [FormerlySerializedAs("ReservedCommandId")]
        public string ReservationCommandId;

        public bool IsReserved =>
            !string.IsNullOrWhiteSpace(ReservationOwnerId) &&
            !string.IsNullOrWhiteSpace(ReservationCommandId);
        public bool IsAvailable => Card != null && !IsReserved;

        public string ReservedCombatSessionId
        {
            get => ReservationOwnerId;
            set => ReservationOwnerId = value;
        }

        public string ReservedCommandId
        {
            get => ReservationCommandId;
            set => ReservationCommandId = value;
        }

        public void ClearReservation()
        {
            ReservationOwnerId = null;
            ReservationCommandId = null;
        }
    }
}
