using System;

namespace CryingSnow.StackCraft
{
    public enum WorldQuestStatus
    {
        Available,
        Active,
        ReadyToTurnIn,
        Completed
    }

    [Serializable]
    public sealed class WorldQuestStateData
    {
        public string QuestId;
        public WorldQuestStatus Status;
        public int ObjectiveIndex;
        public int CurrentAmount;
        public bool AcceptanceRewardClaimed;
        public bool CompletionRewardClaimed;
    }
}
