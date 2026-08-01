using System;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public static class WorldQuestConditionEvaluator
    {
        public static bool Evaluate(
            WorldQuestConditionSet conditionSet,
            GameData gameData,
            WorldQuestRegistry registry,
            WorldQuestStateData currentState = null)
        {
            if (conditionSet == null ||
                conditionSet.Conditions == null ||
                conditionSet.Conditions.Count == 0)
            {
                return conditionSet?.InvertResult != true;
            }

            bool result = conditionSet.Mode == WorldQuestConditionMode.All
                ? conditionSet.Conditions.All(condition =>
                    Evaluate(condition, gameData, registry, currentState))
                : conditionSet.Conditions.Any(condition =>
                    Evaluate(condition, gameData, registry, currentState));
            return conditionSet.InvertResult ? !result : result;
        }

        public static bool Evaluate(
            WorldQuestConditionDefinition condition,
            GameData gameData,
            WorldQuestRegistry registry,
            WorldQuestStateData currentState = null)
        {
            if (condition == null || gameData == null)
                return false;

            switch (condition.Type)
            {
                case WorldQuestConditionType.QuestStatus:
                    return Compare(
                        (int)GetQuestState(gameData, condition.TargetId).Status,
                        (int)condition.QuestStatusValue,
                        condition.Comparison);
                case WorldQuestConditionType.QuestOutcome:
                    return Compare(
                        GetQuestState(gameData, condition.TargetId).OutcomeId ??
                        string.Empty,
                        condition.StringValue ?? string.Empty,
                        condition.Comparison);
                case WorldQuestConditionType.WorldFactBool:
                    return Compare(
                        GetFact(gameData, condition.TargetId)?.BoolValue == true,
                        condition.BoolValue,
                        condition.Comparison);
                case WorldQuestConditionType.WorldFactInt:
                    return Compare(
                        GetFact(gameData, condition.TargetId)?.IntValue ?? 0,
                        condition.IntValue,
                        condition.Comparison);
                case WorldQuestConditionType.WorldFactString:
                    return Compare(
                        GetFact(gameData, condition.TargetId)?.StringValue ??
                        string.Empty,
                        condition.StringValue ?? string.Empty,
                        condition.Comparison);
                case WorldQuestConditionType.ProtagonistLevel:
                    return Compare(
                        gameData.GetProtagonistData()?.Level ?? 0,
                        condition.IntValue,
                        condition.Comparison);
                case WorldQuestConditionType.OwnedCardCount:
                    return Compare(
                        CountOwnedCards(gameData, condition.TargetId),
                        condition.IntValue,
                        condition.Comparison);
                case WorldQuestConditionType.CurrentLocation:
                    return Compare(
                        gameData.ActiveLocationId ?? string.Empty,
                        condition.TargetId ?? string.Empty,
                        condition.Comparison);
                case WorldQuestConditionType.WorldDay:
                    return Compare(
                        gameData.WorldDay <= 0 ? 1 : gameData.WorldDay,
                        condition.IntValue,
                        condition.Comparison);
                case WorldQuestConditionType.SelectedChoiceEquals:
                    return Compare(
                        currentState?.SelectedChoiceId ?? string.Empty,
                        condition.StringValue ?? string.Empty,
                        condition.Comparison);
                default:
                    return false;
            }
        }

        private static WorldQuestStateData GetQuestState(
            GameData gameData,
            string questId)
        {
            return gameData.WorldQuests?.FirstOrDefault(state =>
                       state != null && state.QuestId == questId) ??
                   new WorldQuestStateData
                   {
                       QuestId = questId,
                       Status = WorldQuestStatus.Locked
                   };
        }

        private static WorldFactData GetFact(
            GameData gameData,
            string key)
        {
            return gameData.WorldFacts?.FirstOrDefault(fact =>
                fact != null && fact.Key == key);
        }

        internal static int CountOwnedCards(GameData gameData, string cardId)
        {
            CardDefinition definition =
                CardManager.Instance?.GetDefinitionById(cardId);
            if (definition != null)
                return CardManager.Instance.CountOwnedCard(definition);

            int backpackCount = gameData.Backpack?.Entries?.Count(entry =>
                entry?.Card != null && entry.Card.Id == cardId) ?? 0;
            int partyCount = gameData.PartyMembers?.Count(card =>
                card != null && card.Id == cardId) ?? 0;
            string sceneScope = gameData.GetCurrentSceneScope();
            int tableCount = gameData.SavedScenes != null &&
                gameData.SavedScenes.TryGetValue(
                    sceneScope,
                    out SceneData scene)
                ? scene.SavedStacks.Sum(stack => stack?.Cards?.Count(card =>
                    card != null && card.Id == cardId) ?? 0)
                : 0;
            return backpackCount + partyCount + tableCount;
        }

        private static bool Compare(
            int left,
            int right,
            WorldQuestComparison comparison)
        {
            return comparison switch
            {
                WorldQuestComparison.Equal => left == right,
                WorldQuestComparison.NotEqual => left != right,
                WorldQuestComparison.Less => left < right,
                WorldQuestComparison.LessOrEqual => left <= right,
                WorldQuestComparison.Greater => left > right,
                WorldQuestComparison.GreaterOrEqual => left >= right,
                _ => false
            };
        }

        private static bool Compare(
            bool left,
            bool right,
            WorldQuestComparison comparison)
        {
            return comparison switch
            {
                WorldQuestComparison.Equal => left == right,
                WorldQuestComparison.NotEqual => left != right,
                _ => false
            };
        }

        private static bool Compare(
            string left,
            string right,
            WorldQuestComparison comparison)
        {
            int result = string.Compare(
                left,
                right,
                StringComparison.Ordinal);
            return comparison switch
            {
                WorldQuestComparison.Equal => result == 0,
                WorldQuestComparison.NotEqual => result != 0,
                _ => false
            };
        }
    }
}
