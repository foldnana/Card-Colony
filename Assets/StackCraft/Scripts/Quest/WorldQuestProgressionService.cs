using System;
using System.Collections.Generic;
using System.Linq;

namespace CryingSnow.StackCraft
{
    public static class RiverbendForestQuestRules
    {
        public const string QuestId =
            "story_riverbend_whispering_forest_01";
        public const string GiverNpcId =
            "riverbend-village-chief";
        public const string MarketId = "riverbend-market";
        public const string CommodityId = "food";
        public const string ForestLocationId = "whispering-forest";
        public const string SlimeCardId =
            "366be2e0e40c4b4d93229a94ae7133ae";
        public const int PurchaseObjectiveIndex = 0;
        public const int EnterForestObjectiveIndex = 1;
        public const int DefeatSlimeObjectiveIndex = 2;
        public const int ReturnToChiefObjectiveIndex = 3;
    }

    public static class WorldQuestProgressionService
    {
        public static WorldQuestStateData EnsureQuestState(
            GameData gameData,
            string questId)
        {
            if (gameData == null)
                throw new ArgumentNullException(nameof(gameData));
            if (string.IsNullOrWhiteSpace(questId))
                throw new ArgumentException(
                    "Quest id is required.",
                    nameof(questId));

            gameData.WorldQuests ??= new List<WorldQuestStateData>();
            WorldQuestStateData state = gameData.WorldQuests
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.QuestId == questId);
            if (state == null)
            {
                state = new WorldQuestStateData
                {
                    QuestId = questId,
                    Status = WorldQuestStatus.Available
                };
                gameData.WorldQuests.Add(state);
            }

            MergeDuplicateStates(gameData.WorldQuests, state);
            Normalize(state);
            gameData.WorldQuestStateVersion =
                GameData.CurrentWorldQuestStateVersion;
            return state;
        }

        public static bool TryAccept(GameData gameData, string questId)
        {
            WorldQuestStateData state = EnsureQuestState(gameData, questId);
            if (!IsSupportedQuest(questId) ||
                state.Status != WorldQuestStatus.Available)
            {
                return false;
            }

            state.Status = WorldQuestStatus.Active;
            state.ObjectiveIndex =
                RiverbendForestQuestRules.PurchaseObjectiveIndex;
            state.CurrentAmount = 0;
            return true;
        }

        public static bool ReportMarketPurchase(
            GameData gameData,
            string questId,
            string marketId,
            string commodityId,
            bool playerBuys,
            int quantity)
        {
            WorldQuestStateData state = EnsureQuestState(gameData, questId);
            if (!IsActiveObjective(
                    state,
                    RiverbendForestQuestRules.PurchaseObjectiveIndex) ||
                !playerBuys ||
                quantity <= 0 ||
                marketId != RiverbendForestQuestRules.MarketId ||
                commodityId != RiverbendForestQuestRules.CommodityId)
            {
                return false;
            }

            Advance(
                state,
                RiverbendForestQuestRules.EnterForestObjectiveIndex);
            return true;
        }

        public static bool ReportLocationEntered(
            GameData gameData,
            string questId,
            string locationId)
        {
            WorldQuestStateData state = EnsureQuestState(gameData, questId);
            if (!IsActiveObjective(
                    state,
                    RiverbendForestQuestRules.EnterForestObjectiveIndex) ||
                locationId != RiverbendForestQuestRules.ForestLocationId)
            {
                return false;
            }

            Advance(
                state,
                RiverbendForestQuestRules.DefeatSlimeObjectiveIndex);
            return true;
        }

        public static bool ReportEnemyDefeated(
            GameData gameData,
            string questId,
            string cardDefinitionId,
            bool creditedToPlayerParty)
        {
            WorldQuestStateData state = EnsureQuestState(gameData, questId);
            if (!IsActiveObjective(
                    state,
                    RiverbendForestQuestRules.DefeatSlimeObjectiveIndex) ||
                !creditedToPlayerParty ||
                cardDefinitionId != RiverbendForestQuestRules.SlimeCardId)
            {
                return false;
            }

            state.CurrentAmount = 1;
            state.ObjectiveIndex =
                RiverbendForestQuestRules.ReturnToChiefObjectiveIndex;
            state.Status = WorldQuestStatus.ReadyToTurnIn;
            return true;
        }

        public static bool CanTurnIn(
            GameData gameData,
            string questId,
            string npcId)
        {
            WorldQuestStateData state = EnsureQuestState(gameData, questId);
            return IsSupportedQuest(questId) &&
                   state.Status == WorldQuestStatus.ReadyToTurnIn &&
                   state.ObjectiveIndex ==
                   RiverbendForestQuestRules.ReturnToChiefObjectiveIndex &&
                   npcId == RiverbendForestQuestRules.GiverNpcId;
        }

        public static bool TryComplete(
            GameData gameData,
            string questId,
            string npcId)
        {
            if (!CanTurnIn(gameData, questId, npcId))
                return false;

            WorldQuestStateData state = EnsureQuestState(gameData, questId);
            state.Status = WorldQuestStatus.Completed;
            state.CurrentAmount = 1;
            return true;
        }

        private static bool IsSupportedQuest(string questId)
        {
            return questId == RiverbendForestQuestRules.QuestId;
        }

        private static bool IsActiveObjective(
            WorldQuestStateData state,
            int objectiveIndex)
        {
            return state.Status == WorldQuestStatus.Active &&
                   state.ObjectiveIndex == objectiveIndex;
        }

        private static void Advance(
            WorldQuestStateData state,
            int nextObjectiveIndex)
        {
            state.ObjectiveIndex = nextObjectiveIndex;
            state.CurrentAmount = 0;
        }

        private static void Normalize(WorldQuestStateData state)
        {
            state.ObjectiveIndex = Math.Max(
                0,
                Math.Min(
                    RiverbendForestQuestRules.ReturnToChiefObjectiveIndex,
                    state.ObjectiveIndex));
            state.CurrentAmount = Math.Max(0, state.CurrentAmount);
            if (state.Status == WorldQuestStatus.Available)
            {
                state.ObjectiveIndex = 0;
                state.CurrentAmount = 0;
            }
        }

        private static void MergeDuplicateStates(
            List<WorldQuestStateData> states,
            WorldQuestStateData destination)
        {
            for (int index = states.Count - 1; index >= 0; index--)
            {
                WorldQuestStateData candidate = states[index];
                if (candidate == null ||
                    ReferenceEquals(candidate, destination) ||
                    candidate.QuestId != destination.QuestId)
                {
                    continue;
                }

                if (candidate.Status > destination.Status ||
                    candidate.Status == destination.Status &&
                    candidate.ObjectiveIndex > destination.ObjectiveIndex)
                {
                    destination.Status = candidate.Status;
                    destination.ObjectiveIndex = candidate.ObjectiveIndex;
                    destination.CurrentAmount = candidate.CurrentAmount;
                }

                destination.AcceptanceRewardClaimed |=
                    candidate.AcceptanceRewardClaimed;
                destination.CompletionRewardClaimed |=
                    candidate.CompletionRewardClaimed;
                states.RemoveAt(index);
            }
        }
    }
}
