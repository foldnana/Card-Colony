using System;
using System.Collections.Generic;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public enum WorldQuestObjectiveType
    {
        MarketPurchase,
        EnterLocation,
        DefeatCard,
        ReturnToNpc
    }

    [Serializable]
    public sealed class WorldQuestObjectiveDefinition
    {
        [SerializeField] private WorldQuestObjectiveType type;
        [SerializeField] private string targetId;
        [SerializeField] private string contextId;
        [SerializeField, Min(1)] private int requiredAmount = 1;
        [SerializeField] private string text;

        public WorldQuestObjectiveType Type => type;
        public string TargetId => targetId;
        public string ContextId => contextId;
        public int RequiredAmount => Mathf.Max(1, requiredAmount);
        public string Text => text;
    }

    [Serializable]
    public sealed class WorldQuestRewardDefinition
    {
        [SerializeField] private string cardDefinitionId;
        [SerializeField, Min(0)] private int cardAmount;
        [SerializeField, Min(0)] private int protagonistExperience;

        public string CardDefinitionId => cardDefinitionId;
        public int CardAmount => Mathf.Max(0, cardAmount);
        public int ProtagonistExperience =>
            Mathf.Max(0, protagonistExperience);
    }

    [CreateAssetMenu(
        menuName = "StackCraft/World Quest",
        fileName = "WorldQuest_")]
    public sealed class WorldQuestDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private string giverNpcId;
        [SerializeField] private string acceptLocationId;
        [SerializeField] private List<WorldQuestObjectiveDefinition>
            objectives = new();
        [SerializeField] private List<WorldQuestRewardDefinition>
            acceptanceRewards = new();
        [SerializeField] private List<WorldQuestRewardDefinition>
            completionRewards = new();

        public string Id => id;
        public string Title => title;
        public string Description => description;
        public string GiverNpcId => giverNpcId;
        public string AcceptLocationId => acceptLocationId;
        public IReadOnlyList<WorldQuestObjectiveDefinition> Objectives =>
            objectives;
        public IReadOnlyList<WorldQuestRewardDefinition>
            AcceptanceRewards => acceptanceRewards;
        public IReadOnlyList<WorldQuestRewardDefinition>
            CompletionRewards => completionRewards;
    }
}
