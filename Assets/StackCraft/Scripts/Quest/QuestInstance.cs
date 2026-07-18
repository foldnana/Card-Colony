namespace CryingSnow.StackCraft
{
    public enum QuestStatus
    {
        Inactive, // Hasn't met prerequisites
        Active,   // Currently in progress
        Completed // Finished
    }

    [System.Serializable]
    public class QuestInstance
    {
        public Quest QuestData { get; private set; }
        public QuestStatus Status { get; set; }
        public int CurrentAmount { get; set; }

        public QuestInstance(Quest questData)
        {
            this.QuestData = questData;
            this.Status = QuestStatus.Inactive;
            this.CurrentAmount = 0;
        }

        public void SetProgress(int newAmount)
        {
            CurrentAmount = newAmount;
        }

        public void AddProgress(int amountToAdd)
        {
            CurrentAmount += amountToAdd;
        }

        public bool IsComplete()
        {
            return CurrentAmount >= QuestData.TargetAmount;
        }
    }
}
