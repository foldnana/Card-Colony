using System;
using System.Linq;

namespace CryingSnow.StackCraft
{
    /// <summary>
    /// Performs backpack-to-character equipment changes on serialized data as
    /// one transaction. World visuals are synchronized separately by the view.
    /// </summary>
    public static class BackpackEquipmentTransfer
    {
        public static bool TryEquip(
            BackpackData backpack,
            CardData member,
            string entryId,
            Func<string, EquipmentSlot?> resolveSlot)
        {
            if (backpack == null || member == null || resolveSlot == null ||
                string.IsNullOrWhiteSpace(entryId))
            {
                return false;
            }

            BackpackEntryData entry = backpack.Find(entryId);
            if (entry?.Card == null || entry.IsReserved)
                return false;

            EquipmentSlot? slot = resolveSlot(entry.Card.Id);
            if (!slot.HasValue)
                return false;

            member.EquippedItems ??= new System.Collections.Generic.List<CardData>();
            CardData previous = member.EquippedItems.FirstOrDefault(item =>
                item != null && resolveSlot(item.Id) == slot);
            int previousIndex = previous == null
                ? -1
                : member.EquippedItems.IndexOf(previous);

            if (!backpack.TryRemove(entryId, out CardData incoming))
                return false;

            if (previousIndex >= 0)
                member.EquippedItems.RemoveAt(previousIndex);

            if (previous != null && !backpack.TryAdd(previous, out _))
            {
                member.EquippedItems.Insert(previousIndex, previous);
                backpack.Entries.Add(entry);
                backpack.Normalize();
                return false;
            }

            member.EquippedItems.Add(incoming);
            return true;
        }

        public static bool TryUnequip(
            BackpackData backpack,
            CardData member,
            EquipmentSlot slot,
            Func<string, EquipmentSlot?> resolveSlot)
        {
            if (backpack == null || member?.EquippedItems == null ||
                resolveSlot == null)
            {
                return false;
            }

            CardData equipped = member.EquippedItems.FirstOrDefault(item =>
                item != null && resolveSlot(item.Id) == slot);
            if (equipped == null || !backpack.TryAdd(equipped, out _))
                return false;

            member.EquippedItems.Remove(equipped);
            return true;
        }
    }
}
