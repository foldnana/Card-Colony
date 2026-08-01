namespace CryingSnow.StackCraft
{
    // Kept as a compatibility facade. Rewards now come exclusively from
    // WorldQuestEffectDefinition inside the engine transaction.
    public static class WorldQuestRewardService
    {
        public static bool TryAcceptAndGrant(
            GameData gameData,
            string questId)
        {
            return WorldQuestProgressionService.TryAccept(
                gameData,
                questId);
        }

        public static bool TryCompleteAndGrant(
            GameData gameData,
            string questId,
            string npcId)
        {
            return WorldQuestProgressionService.TryComplete(
                gameData,
                questId,
                npcId);
        }
    }
}
