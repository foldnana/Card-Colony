using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [SerializeField] private DialoguePanelView dialoguePanel;

        private CardInstance player;
        private CardInstance npc;
        private string activeWorldQuestId;
        private string activeWorldQuestOutcomeId;

        public bool IsActive { get; private set; }
        public CardInstance Player => player;
        public CardInstance Npc => npc;
        public CombatRect InteractionRect =>
            NpcInteractionManager.Instance?.InteractionRect;
        public bool HasActiveParticipantAnimation =>
            NpcInteractionManager.Instance
                ?.HasActiveParticipantAnimation == true;
        public IEnumerable<CardInstance> Participants =>
            NpcInteractionManager.Instance?.Participants ??
            System.Array.Empty<CardInstance>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            NpcInteractionManager.Ensure(gameObject);
            dialoguePanel?.Hide();

            if (GameDirector.Instance != null)
                GameDirector.Instance.OnBeforeSave += HandleBeforeSave;
        }

        private void OnDestroy()
        {
            if (GameDirector.Instance != null)
                GameDirector.Instance.OnBeforeSave -= HandleBeforeSave;

            EndDialogueForInteractionEnd();
            if (Instance == this)
                Instance = null;
        }

        private void HandleBeforeSave(GameData gameData)
        {
            if (NpcInteractionManager.Instance?.IsActive == true)
                NpcInteractionManager.Instance.EndInteraction();
            else
                EndDialogueForInteractionEnd();
        }

        public static bool CanStartDialogue(
            CardInstance first,
            CardInstance second)
        {
            if (first == null || second == null || first == second)
                return false;

            return IsPlayerCharacter(first) && IsDialogueNpc(second) ||
                IsPlayerCharacter(second) && IsDialogueNpc(first);
        }

        public bool TryStartDialogueFromDrop(
            CardInstance droppedCard,
            float searchRadius)
        {
            NpcInteractionManager interaction =
                NpcInteractionManager.Ensure(gameObject);
            if (interaction == null ||
                interaction.IsActive ||
                !interaction.TryStartInteractionFromDrop(
                    droppedCard,
                    searchRadius))
            {
                return false;
            }

            if (interaction.BeginDialogue(out _))
                return true;

            interaction.EndInteraction();
            return false;
        }

        public bool StartDialogue(
            CardInstance first,
            CardInstance second)
        {
            NpcInteractionManager interaction =
                NpcInteractionManager.Ensure(gameObject);
            if (interaction == null)
                return false;

            if (!interaction.IsActive)
                return false;

            bool matchesPlayer =
                interaction.Player == first ||
                interaction.Player == second;
            bool matchesNpc =
                interaction.Npc == first ||
                interaction.Npc == second;
            if (!matchesPlayer || !matchesNpc)
                return false;

            return interaction.BeginDialogue(out _);
        }

        internal bool StartDialogueInSession(
            CardInstance first,
            CardInstance second)
        {
            if (IsActive ||
                dialoguePanel == null ||
                !CanStartDialogue(first, second))
            {
                return false;
            }

            player = IsPlayerCharacter(first) ? first : second;
            npc = IsDialogueNpc(first) ? first : second;
            IsActive = true;
            if (TryShowWorldQuestDialogue())
                return true;

            dialoguePanel.Show(
                npc.Definition,
                SelectReply,
                EndDialogue);
            return true;
        }

        private bool TryShowWorldQuestDialogue()
        {
            WorldQuestRuntime runtime = WorldQuestRuntime.Instance;
            string npcId = npc?.Definition?.Id;
            if (runtime == null || string.IsNullOrWhiteSpace(npcId))
            {
                return false;
            }

            runtime.ResolveNpcInteraction(
                npcId,
                GameDirector.Instance?.GameData?.ActiveLocationId ??
                string.Empty);
            IReadOnlyList<WorldQuestViewModel> interactions =
                runtime.GetNpcInteractions(npcId);
            if (interactions.Count == 0)
                return false;
            if (interactions.Count > 1)
            {
                ShowWorldQuestSelection(interactions);
                return true;
            }

            return ShowWorldQuestInteraction(interactions[0]);
        }

        private void ShowWorldQuestSelection(
            IReadOnlyList<WorldQuestViewModel> interactions)
        {
            var options = interactions
                .Select(quest => new DialogueChoiceOption(
                    quest.Title,
                    () => ShowWorldQuestInteraction(quest)))
                .Concat(new[]
                {
                    new DialogueChoiceOption("告辞", EndDialogue)
                })
                .ToList();
            dialoguePanel.ShowChoices(
                npc.Definition,
                "这里有多项事务与你有关。",
                "选择要谈的事情",
                options);
        }

        private bool ShowWorldQuestInteraction(WorldQuestViewModel quest)
        {
            WorldQuestRuntime runtime = WorldQuestRuntime.Instance;
            WorldQuestDefinition definition = runtime.GetDefinition(
                quest.QuestId);
            if (definition == null)
                return false;
            activeWorldQuestId = quest.QuestId;
            switch (quest.Status)
            {
                case WorldQuestStatus.Available:
                    dialoguePanel.ShowQuest(
                        npc.Definition,
                        definition.Dialogue.OfferText,
                        definition.Dialogue.AcceptLabel,
                        AcceptWorldQuest,
                        definition.Dialogue.DeclineLabel,
                        EndDialogue);
                    return true;
                case WorldQuestStatus.ReadyToTurnIn:
                    IReadOnlyList<WorldQuestOutcomeDefinition> outcomes =
                        runtime.GetEligibleOutcomes(quest.QuestId);
                    if (outcomes.Count > 1)
                        ShowWorldQuestOutcomeSelection(quest, outcomes);
                    else
                        ShowWorldQuestOutcomeConfirmation(
                            quest,
                            outcomes.FirstOrDefault());
                    return true;
                case WorldQuestStatus.Active:
                    dialoguePanel.ShowQuest(
                        npc.Definition,
                        definition.FindStage(quest.StageId)
                            ?.ActiveReminderText ?? quest.ObjectiveText,
                        null,
                        null,
                        "告辞",
                        EndDialogue);
                    return true;
                case WorldQuestStatus.Suspended:
                    dialoguePanel.ShowQuest(
                        npc.Definition,
                        definition.Dialogue.SuspendedText,
                        null,
                        null,
                        "告辞",
                        EndDialogue);
                    return true;
                case WorldQuestStatus.Completed:
                    dialoguePanel.ShowQuest(
                        npc.Definition,
                        definition.Dialogue.CompletedText,
                        null,
                        null,
                        "告辞",
                        EndDialogue);
                    return true;
                case WorldQuestStatus.Failed:
                    dialoguePanel.ShowQuest(
                        npc.Definition,
                        definition.Dialogue.FailedText,
                        null,
                        null,
                        "告辞",
                        EndDialogue);
                    return true;
                case WorldQuestStatus.Cancelled:
                    dialoguePanel.ShowQuest(
                        npc.Definition,
                        definition.Dialogue.CancelledText,
                        null,
                        null,
                        "告辞",
                        EndDialogue);
                    return true;
                default:
                    return false;
            }
        }

        private void ShowWorldQuestOutcomeSelection(
            WorldQuestViewModel quest,
            IReadOnlyList<WorldQuestOutcomeDefinition> outcomes)
        {
            var options = outcomes
                .Select(outcome => new DialogueChoiceOption(
                    outcome.ChoiceLabel,
                    () => ShowWorldQuestOutcomeConfirmation(
                        quest,
                        outcome)))
                .Concat(new[]
                {
                    new DialogueChoiceOption("暂不决定", EndDialogue)
                })
                .ToList();
            dialoguePanel.ShowChoices(
                npc.Definition,
                "请选择这次任务的处理方式。",
                "你的决定",
                options);
        }

        private void ShowWorldQuestOutcomeConfirmation(
            WorldQuestViewModel quest,
            WorldQuestOutcomeDefinition outcome)
        {
            WorldQuestDefinition definition = WorldQuestRuntime.Instance
                ?.GetDefinition(quest.QuestId);
            activeWorldQuestId = quest.QuestId;
            activeWorldQuestOutcomeId = outcome?.OutcomeId;
            dialoguePanel.ShowQuest(
                npc.Definition,
                outcome?.ResolutionText ?? quest.ObjectiveText,
                outcome?.ChoiceLabel ?? definition?.Dialogue.TurnInLabel,
                outcome == null ? null : TurnInWorldQuest,
                definition?.Dialogue.PostponeLabel ?? "暂不决定",
                EndDialogue);
        }

        private void AcceptWorldQuest()
        {
            if (!IsActive)
                return;

            WorldQuestOperationResult result =
                WorldQuestRuntime.Instance?.TryAccept(activeWorldQuestId) ??
                WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.NotInitialized,
                    activeWorldQuestId,
                    string.Empty);
            dialoguePanel?.ShowResponse(
                result.Success
                    ? WorldQuestRuntime.Instance
                        ?.GetDefinition(activeWorldQuestId)
                        ?.Dialogue.AcceptedText
                    : "委托暂时无法登记，请稍后再试。");
        }

        private void TurnInWorldQuest()
        {
            if (!IsActive)
                return;

            WorldQuestRuntime runtime = WorldQuestRuntime.Instance;
            WorldQuestOperationResult result = runtime?.TryTurnIn(
                activeWorldQuestId,
                activeWorldQuestOutcomeId) ??
                WorldQuestOperationResult.Fail(
                    WorldQuestResultCode.NotInitialized,
                    activeWorldQuestId,
                    string.Empty);
            dialoguePanel?.ShowResponse(
                result.Success
                    ? "辛苦了。这份报酬是你应得的。"
                    : "调查记录还不完整，准备好后再来找我。");
        }

        public bool IsCardInDialogue(CardInstance card)
        {
            return IsActive &&
                card != null &&
                (card == player || card == npc);
        }

        public void SelectReply()
        {
            if (!IsActive || npc?.Definition == null)
                return;

            dialoguePanel?.ShowResponse(
                npc.Definition.DialogueResponseText);
        }

        public void EndDialogue()
        {
            if (!IsActive)
                return;

            HideDialogue();
            NpcInteractionManager.Instance?.HandleDialogueEnded();
        }

        internal void EndDialogueForInteractionEnd()
        {
            if (!IsActive)
                return;

            HideDialogue();
        }

        private void HideDialogue()
        {
            IsActive = false;
            dialoguePanel?.Hide();
            player = null;
            npc = null;
            activeWorldQuestId = null;
            activeWorldQuestOutcomeId = null;
        }

        private static bool IsPlayerCharacter(CardInstance card)
        {
            return card?.Definition != null &&
                card.Definition.Category == CardCategory.Character &&
                card.Definition.Faction == CardFaction.Player;
        }

        private static bool IsDialogueNpc(CardInstance card)
        {
            return card?.Definition != null &&
                card.Definition.Category == CardCategory.Character &&
                card.Definition.Faction == CardFaction.Neutral &&
                card.Definition.DialogueEnabled;
        }
    }
}
