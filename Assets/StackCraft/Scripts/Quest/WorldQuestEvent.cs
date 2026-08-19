using System;
using System.Collections.Generic;

namespace CryingSnow.StackCraft
{
    public enum WorldQuestEventType
    {
        NpcTalked = 0,
        MarketTradeCommitted = 1,
        LocationEntered = 2,
        CardDefeated = 3,
        CardObtained = 4,
        CardDelivered = 5,
        CardCrafted = 6,
        InventorySnapshotChanged = 7,
        ProtagonistLevelChanged = 8,
        WorldDayStarted = 9,
        DialogueChoiceSelected = 10,
        GameplayInteraction = 11
    }

    public enum WorldQuestTradeDirection
    {
        None = 0,
        PlayerBuys = 1,
        PlayerSells = 2
    }

    public readonly struct WorldQuestEvent
    {
        public string EventId { get; }
        public WorldQuestEventType Type { get; }
        public long WorldHour { get; }
        public string LocationId { get; }
        public string ActorPersistentId { get; }
        public bool CreditedToPlayerParty { get; }
        public bool ProtagonistParticipated { get; }
        public string PrimaryTargetId { get; }
        public string SecondaryTargetId { get; }
        public string ContextId { get; }
        public int Amount { get; }
        public WorldQuestTradeDirection TradeDirection { get; }
        public GameplayInteractionPhase InteractionPhase { get; }
        public string ActionId =>
            Type == WorldQuestEventType.GameplayInteraction
                ? PrimaryTargetId
                : string.Empty;
        public string InteractionOutcomeId =>
            Type == WorldQuestEventType.GameplayInteraction
                ? SecondaryTargetId
                : string.Empty;

        public WorldQuestEvent(
            string eventId,
            WorldQuestEventType type,
            long worldHour,
            string locationId,
            string actorPersistentId,
            bool creditedToPlayerParty,
            bool protagonistParticipated,
            string primaryTargetId,
            string secondaryTargetId,
            string contextId,
            int amount,
            WorldQuestTradeDirection tradeDirection)
            : this(
                eventId,
                type,
                worldHour,
                locationId,
                actorPersistentId,
                creditedToPlayerParty,
                protagonistParticipated,
                primaryTargetId,
                secondaryTargetId,
                contextId,
                amount,
                tradeDirection,
                GameplayInteractionPhase.Resolved)
        {
        }

        public WorldQuestEvent(
            string eventId,
            WorldQuestEventType type,
            long worldHour,
            string locationId,
            string actorPersistentId,
            bool creditedToPlayerParty,
            bool protagonistParticipated,
            string primaryTargetId,
            string secondaryTargetId,
            string contextId,
            int amount,
            WorldQuestTradeDirection tradeDirection,
            GameplayInteractionPhase interactionPhase)
        {
            EventId = eventId;
            Type = type;
            WorldHour = worldHour;
            LocationId = locationId;
            ActorPersistentId = actorPersistentId;
            CreditedToPlayerParty = creditedToPlayerParty;
            ProtagonistParticipated = protagonistParticipated;
            PrimaryTargetId = primaryTargetId;
            SecondaryTargetId = secondaryTargetId;
            ContextId = contextId;
            Amount = amount;
            TradeDirection = tradeDirection;
            InteractionPhase = interactionPhase;
        }
    }

    public enum WorldQuestResultCode
    {
        Success = 0,
        NoChange = 1,
        NotInitialized = 2,
        InvalidArgument = 3,
        DefinitionNotFound = 4,
        StateNotFound = 5,
        InvalidStateTransition = 6,
        ConditionsNotMet = 7,
        EventRejected = 8,
        ChoiceAlreadyLocked = 9,
        NoEligibleOutcome = 10,
        OutcomeNotEligible = 11,
        ResourceNotFound = 12,
        InventoryCapacityInsufficient = 13,
        TransactionCycleDetected = 14,
        TransactionRolledBack = 15,
        DefinitionInvalid = 16,
        ActiveQuestLimitReached = 17,
        ReentrantOperationRejected = 18
    }

    public readonly struct WorldQuestOperationResult
    {
        public bool Success { get; }
        public WorldQuestResultCode Code { get; }
        public string QuestId { get; }
        public string Message { get; }
        public IReadOnlyList<string> ChangedQuestIds { get; }

        public WorldQuestOperationResult(
            bool success,
            WorldQuestResultCode code,
            string questId,
            string message,
            IReadOnlyList<string> changedQuestIds = null)
        {
            Success = success;
            Code = code;
            QuestId = questId;
            Message = message ?? string.Empty;
            ChangedQuestIds = changedQuestIds ?? Array.Empty<string>();
        }

        public static WorldQuestOperationResult Ok(
            string questId,
            params string[] changedQuestIds)
        {
            return new WorldQuestOperationResult(
                true,
                WorldQuestResultCode.Success,
                questId,
                string.Empty,
                changedQuestIds);
        }

        public static WorldQuestOperationResult NoChange(
            string questId,
            string message = "")
        {
            return new WorldQuestOperationResult(
                true,
                WorldQuestResultCode.NoChange,
                questId,
                message);
        }

        public static WorldQuestOperationResult Fail(
            WorldQuestResultCode code,
            string questId,
            string message)
        {
            return new WorldQuestOperationResult(
                false,
                code,
                questId,
                message);
        }
    }
}
