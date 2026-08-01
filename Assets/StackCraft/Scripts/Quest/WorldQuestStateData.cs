using System;
using System.Collections.Generic;

namespace CryingSnow.StackCraft
{
    public enum WorldQuestStatus
    {
        Locked = 0,
        Available = 1,
        Active = 2,
        Suspended = 3,
        ReadyToTurnIn = 4,
        Completed = 5,
        Failed = 6,
        Cancelled = 7
    }

    [Serializable]
    public sealed class WorldQuestObjectiveProgressData
    {
        public string ObjectiveId;
        public int CurrentAmount;
        public bool IsComplete;

        public WorldQuestObjectiveProgressData Clone()
        {
            return new WorldQuestObjectiveProgressData
            {
                ObjectiveId = ObjectiveId,
                CurrentAmount = CurrentAmount,
                IsComplete = IsComplete
            };
        }
    }

    [Serializable]
    public sealed class WorldQuestStateData
    {
        public string QuestId;
        public WorldQuestStatus Status;
        public string ActiveStageId;
        public List<WorldQuestObjectiveProgressData> ObjectiveProgress = new();
        public string SelectedChoiceId;
        public string OutcomeId;
        public string StatusReasonId;
        public WorldQuestStatus SuspendedFromStatus;
        public bool AvailabilityOverride;
        public int RunNumber;
        public int CompletionCount;
        public long AvailableSinceWorldHour = -1;
        public long AcceptedWorldHour = -1;
        public long LastUpdatedWorldHour = -1;
        public long LastCompletedWorldHour = -1;
        public long NextAvailableWorldHour = -1;
        public int Revision;
        public List<string> AppliedOperationIds = new();

        // V1 JSON bridge. Cleared after migration; kept serialized so the V2
        // migrator can read existing saves before their first V2 write.
        [Obsolete] public int ObjectiveIndex;
        [Obsolete] public int CurrentAmount;
        [Obsolete] public bool AcceptanceRewardClaimed;
        [Obsolete] public bool CompletionRewardClaimed;

        public WorldQuestStateData Clone()
        {
            var clone = (WorldQuestStateData)MemberwiseClone();
            clone.ObjectiveProgress = ObjectiveProgress == null
                ? new List<WorldQuestObjectiveProgressData>()
                : ObjectiveProgress.ConvertAll(progress => progress?.Clone());
            clone.AppliedOperationIds = AppliedOperationIds == null
                ? new List<string>()
                : new List<string>(AppliedOperationIds);
            return clone;
        }

        public void RestoreFrom(WorldQuestStateData source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            QuestId = source.QuestId;
            Status = source.Status;
            ActiveStageId = source.ActiveStageId;
            ObjectiveProgress = source.ObjectiveProgress == null
                ? new List<WorldQuestObjectiveProgressData>()
                : source.ObjectiveProgress.ConvertAll(value => value?.Clone());
            SelectedChoiceId = source.SelectedChoiceId;
            OutcomeId = source.OutcomeId;
            StatusReasonId = source.StatusReasonId;
            SuspendedFromStatus = source.SuspendedFromStatus;
            AvailabilityOverride = source.AvailabilityOverride;
            RunNumber = source.RunNumber;
            CompletionCount = source.CompletionCount;
            AvailableSinceWorldHour = source.AvailableSinceWorldHour;
            AcceptedWorldHour = source.AcceptedWorldHour;
            LastUpdatedWorldHour = source.LastUpdatedWorldHour;
            LastCompletedWorldHour = source.LastCompletedWorldHour;
            NextAvailableWorldHour = source.NextAvailableWorldHour;
            Revision = source.Revision;
            AppliedOperationIds = source.AppliedOperationIds == null
                ? new List<string>()
                : new List<string>(source.AppliedOperationIds);
        }
    }

    public enum WorldFactValueType
    {
        Bool = 0,
        Int = 1,
        String = 2
    }

    [Serializable]
    public sealed class WorldFactData
    {
        public string Key;
        public WorldFactValueType Type;
        public bool BoolValue;
        public int IntValue;
        public string StringValue;
        public int Revision;

        public WorldFactData Clone()
        {
            return (WorldFactData)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class LegacyWorldQuestStateDataV1
    {
        public string QuestId;
        public int Status;
        public int ObjectiveIndex;
        public int CurrentAmount;
        public bool AcceptanceRewardClaimed;
        public bool CompletionRewardClaimed;
    }
}
