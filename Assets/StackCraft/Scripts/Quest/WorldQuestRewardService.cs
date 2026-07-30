using System.Collections.Generic;

namespace CryingSnow.StackCraft
{
    public static class WorldQuestRewardService
    {
        public const string CoinDefinitionId =
            "4bda315463bf4b73b63f1d232fb522e4";
        public const string MedicineDefinitionId = "medicine";
        public const int AcceptanceCoinAmount = 8;
        public const int AcceptanceMedicineAmount = 1;
        public const int CompletionCoinAmount = 10;
        public const int CompletionExperienceAmount = 10;

        public static bool TryAcceptAndGrant(
            GameData gameData,
            string questId)
        {
            if (gameData == null)
                return false;

            WorldQuestStateData state =
                WorldQuestProgressionService.EnsureQuestState(
                    gameData,
                    questId);
            if (state.Status != WorldQuestStatus.Available ||
                state.AcceptanceRewardClaimed)
            {
                return false;
            }

            BackpackData backpack = gameData.EnsureBackpack();
            var addedEntries = new List<string>();
            if (!TryAddCards(
                    backpack,
                    CoinDefinitionId,
                    AcceptanceCoinAmount,
                    addedEntries) ||
                !TryAddCards(
                    backpack,
                    MedicineDefinitionId,
                    AcceptanceMedicineAmount,
                    addedEntries))
            {
                RollBack(backpack, addedEntries);
                return false;
            }

            if (!WorldQuestProgressionService.TryAccept(
                    gameData,
                    questId))
            {
                RollBack(backpack, addedEntries);
                return false;
            }

            state.AcceptanceRewardClaimed = true;
            return true;
        }

        public static bool TryCompleteAndGrant(
            GameData gameData,
            string questId,
            string npcId)
        {
            if (gameData == null)
                return false;

            WorldQuestStateData state =
                WorldQuestProgressionService.EnsureQuestState(
                    gameData,
                    questId);
            CardData protagonist = gameData.GetProtagonistData();
            if (state.CompletionRewardClaimed ||
                protagonist == null ||
                !WorldQuestProgressionService.CanTurnIn(
                    gameData,
                    questId,
                    npcId))
            {
                return false;
            }

            BackpackData backpack = gameData.EnsureBackpack();
            var addedEntries = new List<string>();
            if (!TryAddCards(
                    backpack,
                    CoinDefinitionId,
                    CompletionCoinAmount,
                    addedEntries))
            {
                RollBack(backpack, addedEntries);
                return false;
            }

            int previousLevel = protagonist.Level;
            int previousExperience = protagonist.Experience;
            int previousHealth = protagonist.CurrentHealth;
            int previousMaximumHealth = protagonist.MaximumHealth;
            CharacterProgressionService.GrantExperience(
                protagonist,
                CompletionExperienceAmount);
            if (!WorldQuestProgressionService.TryComplete(
                    gameData,
                    questId,
                    npcId))
            {
                protagonist.Level = previousLevel;
                protagonist.Experience = previousExperience;
                protagonist.CurrentHealth = previousHealth;
                protagonist.MaximumHealth = previousMaximumHealth;
                RollBack(backpack, addedEntries);
                return false;
            }

            state.CompletionRewardClaimed = true;
            return true;
        }

        private static bool TryAddCards(
            BackpackData backpack,
            string definitionId,
            int count,
            ICollection<string> addedEntries)
        {
            for (int index = 0; index < count; index++)
            {
                if (!backpack.TryAdd(
                        CreateRewardCard(definitionId),
                        out BackpackEntryData entry))
                {
                    return false;
                }

                addedEntries.Add(entry.InstanceId);
            }

            return true;
        }

        private static CardData CreateRewardCard(string definitionId)
        {
            bool medicine = definitionId == MedicineDefinitionId;
            return new CardData
            {
                Id = definitionId,
                UsesLeft = 1,
                CurrentHealth = 15,
                MaximumHealth = 15,
                CurrentNutrition = medicine ? 3 : 0,
                Level = 1,
                CurrentEnergy = 4,
                MaxEnergy = 4
            };
        }

        private static void RollBack(
            BackpackData backpack,
            IEnumerable<string> addedEntries)
        {
            foreach (string entryId in addedEntries)
                backpack.TryRemove(entryId, out _);
        }
    }
}
