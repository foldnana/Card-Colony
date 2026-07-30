using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public static class ProtagonistRecoveryService
    {
        public const string RescueMedicineDefinitionId = "medicine";

        public static bool CanBeRescued(GameData gameData)
        {
            CardData protagonist = gameData?.GetProtagonistData();
            if (protagonist == null || !protagonist.IsDowned ||
                gameData.PartyMembers == null)
            {
                return false;
            }

            return gameData.PartyMembers.Any(member =>
                member != null &&
                member.PersistentId != protagonist.PersistentId &&
                !member.IsDowned &&
                member.CurrentHealth > 0);
        }

        public static bool Revive(
            CardData protagonist,
            int restoredHealth,
            int restoredEnergy)
        {
            if (protagonist == null || !protagonist.IsDowned ||
                restoredHealth <= 0)
            {
                return false;
            }

            protagonist.NormalizeProgression();
            protagonist.IsDowned = false;
            int maximumHealth = protagonist.MaximumHealth > 0
                ? protagonist.MaximumHealth
                : Mathf.Max(1, restoredHealth);
            protagonist.CurrentHealth = Mathf.Clamp(
                restoredHealth,
                1,
                maximumHealth);
            protagonist.CurrentEnergy = Mathf.Clamp(
                restoredEnergy,
                0,
                protagonist.MaxEnergy);
            return true;
        }

        public static bool TryRescueWithBackpackItem(
            GameData gameData,
            string backpackEntryId)
        {
            if (!CanBeRescued(gameData) ||
                string.IsNullOrWhiteSpace(backpackEntryId) ||
                CardManager.Instance == null)
            {
                return false;
            }

            BackpackData backpack = gameData.EnsureBackpack();
            BackpackEntryData entry = backpack.Find(backpackEntryId);
            if (entry?.Card?.Id != RescueMedicineDefinitionId)
                return false;

            CardDefinition item = entry?.Card == null
                ? null
                : CardManager.Instance.GetDefinitionById(entry.Card.Id);
            if (item == null ||
                item.Category != CardCategory.Consumable ||
                item.Nutrition <= 0)
            {
                return false;
            }

            if (!backpack.TryRemove(backpackEntryId, out _))
                return false;

            CardData protagonist = gameData.GetProtagonistData();
            int restoredHealth = Mathf.Max(1, item.Nutrition);
            Revive(protagonist, restoredHealth, restoredEnergy: 1);
            CardInstance active = GameDirector.Instance
                ?.FindActiveProtagonistCard();
            active?.Revive(restoredHealth, restoredEnergy: 1);
            BackpackService.NotifyContentsChanged();
            return true;
        }

        public static BackpackEntryData FindRescueMedicineEntry(
            GameData gameData)
        {
            return gameData?.EnsureBackpack()?.Entries?.FirstOrDefault(
                entry =>
                    entry?.Card != null &&
                    entry.Card.Id == RescueMedicineDefinitionId);
        }

        public static bool TryRescueWithFirstMedicine(GameData gameData)
        {
            BackpackEntryData entry = FindRescueMedicineEntry(gameData);
            return entry != null &&
                TryRescueWithBackpackItem(gameData, entry.InstanceId);
        }
    }
}
