using System;
using System.Collections.Generic;

namespace CryingSnow.StackCraft
{
    public readonly struct WorldQuestOutcomeViewModel
    {
        public string OutcomeId { get; }
        public string ChoiceLabel { get; }
        public string ResolutionText { get; }
        public int Priority { get; }

        public WorldQuestOutcomeViewModel(
            string outcomeId,
            string choiceLabel,
            string resolutionText,
            int priority)
        {
            OutcomeId = outcomeId;
            ChoiceLabel = choiceLabel;
            ResolutionText = resolutionText;
            Priority = priority;
        }
    }

    public readonly struct WorldQuestListItemViewModel
    {
        public WorldQuestViewModel Quest { get; }
        public string GiverNpcId { get; }
        public string StageTitle { get; }
        public string OutcomeTitle { get; }
        public string OutcomeResolution { get; }

        public WorldQuestListItemViewModel(
            WorldQuestViewModel quest,
            string giverNpcId,
            string stageTitle,
            string outcomeTitle,
            string outcomeResolution)
        {
            Quest = quest;
            GiverNpcId = giverNpcId;
            StageTitle = stageTitle;
            OutcomeTitle = outcomeTitle;
            OutcomeResolution = outcomeResolution;
        }
    }

    public readonly struct WorldQuestTrackerObjectiveViewModel
    {
        public string Text { get; }
        public int CurrentAmount { get; }
        public int RequiredAmount { get; }
        public bool IsComplete { get; }

        public WorldQuestTrackerObjectiveViewModel(
            string text,
            int currentAmount,
            int requiredAmount,
            bool isComplete)
        {
            Text = text;
            CurrentAmount = currentAmount;
            RequiredAmount = requiredAmount;
            IsComplete = isComplete;
        }
    }

    public readonly struct WorldQuestTrackerViewModel
    {
        public bool HasQuest { get; }
        public string QuestId { get; }
        public string Title { get; }
        public WorldQuestCategory Category { get; }
        public WorldQuestStatus Status { get; }
        public string StageTitle { get; }
        public string StatusReasonId { get; }
        public IReadOnlyList<string> TurnInNpcIds { get; }
        public IReadOnlyList<WorldQuestTrackerObjectiveViewModel> Objectives
            { get; }

        public WorldQuestTrackerViewModel(
            string questId,
            string title,
            WorldQuestCategory category,
            WorldQuestStatus status,
            string stageTitle,
            string statusReasonId,
            IReadOnlyList<string> turnInNpcIds,
            IReadOnlyList<WorldQuestTrackerObjectiveViewModel> objectives)
        {
            HasQuest = !string.IsNullOrWhiteSpace(questId);
            QuestId = questId;
            Title = title;
            Category = category;
            Status = status;
            StageTitle = stageTitle;
            StatusReasonId = statusReasonId;
            TurnInNpcIds = turnInNpcIds ?? Array.Empty<string>();
            Objectives = objectives ??
                Array.Empty<WorldQuestTrackerObjectiveViewModel>();
        }
    }

    public interface IWorldQuestRuntime
    {
        void Initialize(GameData gameData);
        WorldQuestOperationResult ReportEvent(WorldQuestEvent questEvent);
        WorldQuestOperationResult TryAccept(string questId);
        IReadOnlyList<WorldQuestOutcomeViewModel> GetEligibleOutcomes(
            string questId);
        WorldQuestOperationResult TryTurnIn(
            string questId,
            string outcomeId);
        WorldQuestOperationResult ResolveNpcInteraction(
            string npcId,
            string locationId);
        WorldQuestOperationResult SetTrackedQuest(string questId);
        WorldQuestOperationResult ClearTrackedQuest();
        WorldQuestStateData GetState(string questId);
        IReadOnlyList<WorldQuestListItemViewModel> GetQuestList();
        WorldQuestTrackerViewModel GetTrackedQuest();
    }
}
