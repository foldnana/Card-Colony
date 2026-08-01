using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    // Compatibility facade for callers that have not yet moved to
    // WorldQuestRuntime.ReportEvent. It contains no quest-specific rules.
    public static class WorldQuestProgressionService
    {
        private static readonly ConditionalWeakTable<
            GameData,
            WorldQuestEngine> Engines = new();

        public static WorldQuestStateData EnsureQuestState(
            GameData gameData,
            string questId)
        {
            if (gameData == null)
                throw new ArgumentNullException(nameof(gameData));
            WorldQuestEngine engine = GetEngine(gameData);
            WorldQuestStateData state = engine.GetState(questId);
            if (state == null)
                throw new ArgumentException(
                    $"Quest definition '{questId}' was not found.",
                    nameof(questId));
            return state.Clone();
        }

        public static bool TryAccept(GameData gameData, string questId)
        {
            return gameData != null &&
                   GetEngine(gameData).TryAccept(questId).Success;
        }

        public static bool ReportMarketPurchase(
            GameData gameData,
            string questId,
            string marketId,
            string commodityId,
            bool playerBuys,
            int quantity)
        {
            if (gameData == null)
                return false;
            return GetEngine(gameData).ReportEvent(new WorldQuestEvent(
                Guid.NewGuid().ToString("N"),
                WorldQuestEventType.MarketTradeCommitted,
                gameData.WorldElapsedHours,
                gameData.ActiveLocationId,
                gameData.ProtagonistPersistentId,
                true,
                true,
                commodityId,
                string.Empty,
                marketId,
                quantity,
                playerBuys
                    ? WorldQuestTradeDirection.PlayerBuys
                    : WorldQuestTradeDirection.PlayerSells)).Code ==
                WorldQuestResultCode.Success;
        }

        public static bool ReportLocationEntered(
            GameData gameData,
            string questId,
            string locationId)
        {
            if (gameData == null)
                return false;
            gameData.ActiveLocationId = locationId;
            WorldQuestEngine engine = GetEngine(gameData);
            return engine.ReportEvent(new WorldQuestEvent(
                Guid.NewGuid().ToString("N"),
                WorldQuestEventType.LocationEntered,
                gameData.WorldElapsedHours,
                locationId,
                gameData.ProtagonistPersistentId,
                true,
                true,
                locationId,
                string.Empty,
                string.Empty,
                1,
                WorldQuestTradeDirection.None)).Code ==
                WorldQuestResultCode.Success;
        }

        public static bool ReportEnemyDefeated(
            GameData gameData,
            string questId,
            string cardDefinitionId,
            bool creditedToPlayerParty)
        {
            if (gameData == null)
                return false;
            WorldQuestEngine engine = GetEngine(gameData);
            return engine.ReportEvent(new WorldQuestEvent(
                Guid.NewGuid().ToString("N"),
                WorldQuestEventType.CardDefeated,
                gameData.WorldElapsedHours,
                gameData.ActiveLocationId,
                gameData.ProtagonistPersistentId,
                creditedToPlayerParty,
                creditedToPlayerParty,
                cardDefinitionId,
                string.Empty,
                string.Empty,
                1,
                WorldQuestTradeDirection.None)).Code ==
                WorldQuestResultCode.Success;
        }

        public static bool ReportNpcTalked(
            GameData gameData,
            string questId,
            string npcId)
        {
            if (gameData == null)
                return false;
            WorldQuestEngine engine = GetEngine(gameData);
            if (engine.Registry.TryGet(
                    questId,
                    out WorldQuestDefinition definition) &&
                !string.IsNullOrWhiteSpace(definition.AcceptLocationId))
            {
                gameData.ActiveLocationId = definition.AcceptLocationId;
            }
            return engine.ReportEvent(new WorldQuestEvent(
                Guid.NewGuid().ToString("N"),
                WorldQuestEventType.NpcTalked,
                gameData.WorldElapsedHours,
                gameData.ActiveLocationId,
                gameData.ProtagonistPersistentId,
                true,
                true,
                npcId,
                string.Empty,
                string.Empty,
                1,
                WorldQuestTradeDirection.None)).Code ==
                WorldQuestResultCode.Success;
        }

        public static bool CanTurnIn(
            GameData gameData,
            string questId,
            string npcId)
        {
            if (gameData == null)
                return false;
            WorldQuestEngine engine = GetEngine(gameData);
            return engine.GetState(questId)?.Status ==
                       WorldQuestStatus.ReadyToTurnIn &&
                   engine.Registry.TryGet(questId, out var definition) &&
                   definition.TurnInNpcIds.Contains(npcId);
        }

        public static bool TryComplete(
            GameData gameData,
            string questId,
            string npcId)
        {
            if (!CanTurnIn(gameData, questId, npcId))
                return false;
            WorldQuestEngine engine = GetEngine(gameData);
            WorldQuestOutcomeDefinition outcome =
                engine.GetEligibleOutcomes(questId).FirstOrDefault();
            return outcome != null &&
                   engine.TryTurnIn(
                       questId,
                       outcome.OutcomeId,
                       npcId).Success;
        }

        private static WorldQuestEngine GetEngine(GameData gameData)
        {
            return Engines.GetValue(gameData, data =>
            {
                var engine = new WorldQuestEngine(
                    data,
                    (IEnumerable<WorldQuestDefinition>)
                    Resources.LoadAll<WorldQuestDefinition>("WorldQuests"));
                engine.Initialize();
                return engine;
            });
        }
    }
}
