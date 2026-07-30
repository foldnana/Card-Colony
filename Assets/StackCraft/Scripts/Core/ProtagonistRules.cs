namespace CryingSnow.StackCraft
{
    public static class ProtagonistRules
    {
        public static bool IsProtagonist(GameData gameData, CardData cardData)
        {
            return gameData != null &&
                cardData != null &&
                !string.IsNullOrWhiteSpace(gameData.ProtagonistPersistentId) &&
                cardData.PersistentId == gameData.ProtagonistPersistentId;
        }

        public static bool IsProtagonist(GameData gameData, CardInstance card)
        {
            return gameData != null &&
                card != null &&
                !string.IsNullOrWhiteSpace(gameData.ProtagonistPersistentId) &&
                card.PersistentId == gameData.ProtagonistPersistentId;
        }

        public static bool IsProtagonist(CardInstance card)
        {
            return IsProtagonist(GameDirector.Instance?.GameData, card);
        }

        public static bool CanBeStored(CardInstance card)
        {
            return card != null && !IsProtagonist(card);
        }

        public static bool CanBeSold(CardInstance card)
        {
            return card != null && !IsProtagonist(card);
        }

        public static bool CanBeConsumed(CardInstance card)
        {
            return card != null && !IsProtagonist(card);
        }

        public static bool ShouldEnterDownedState(CardInstance card)
        {
            return IsProtagonist(card);
        }
    }
}
