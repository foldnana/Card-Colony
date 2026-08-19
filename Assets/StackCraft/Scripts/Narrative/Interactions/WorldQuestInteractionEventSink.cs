namespace CryingSnow.StackCraft
{
    public sealed class WorldQuestInteractionEventSink :
        IGameplayInteractionEventSink
    {
        private readonly IWorldQuestRuntime questRuntime;
        private readonly GameData gameData;

        public WorldQuestInteractionEventSink(
            IWorldQuestRuntime questRuntime,
            GameData gameData)
        {
            this.questRuntime = questRuntime;
            this.gameData = gameData;
        }

        public void Publish(GameplayInteractionEventData interactionEvent)
        {
            if (interactionEvent == null || questRuntime == null)
                return;

            questRuntime.ReportEvent(new WorldQuestEvent(
                interactionEvent.PersistedEventId,
                WorldQuestEventType.GameplayInteraction,
                gameData?.WorldElapsedHours ?? 0,
                gameData?.ActiveLocationId ?? string.Empty,
                gameData?.ProtagonistPersistentId ?? string.Empty,
                interactionEvent.CreditedToParty,
                interactionEvent.ProtagonistParticipated,
                interactionEvent.ActionId,
                interactionEvent.OutcomeId,
                interactionEvent.ContextId,
                interactionEvent.Quantity,
                WorldQuestTradeDirection.None,
                interactionEvent.Phase));
        }
    }
}
