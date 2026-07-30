using System.Collections.Generic;
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
            if (npc?.Definition?.Id !=
                    RiverbendForestQuestRules.GiverNpcId ||
                WorldQuestRuntime.Instance == null)
            {
                return false;
            }

            const string questId = RiverbendForestQuestRules.QuestId;
            WorldQuestViewModel quest =
                WorldQuestRuntime.Instance.GetViewModel(questId);
            switch (quest.Status)
            {
                case WorldQuestStatus.Available:
                    dialoguePanel.ShowQuest(
                        npc.Definition,
                        "最近低语森林里总有奇怪的动静。\n" +
                        "去市场准备一点吃的，再替村里看看发生了什么。",
                        "接受委托",
                        AcceptWorldQuest,
                        "暂不接受",
                        EndDialogue);
                    return true;
                case WorldQuestStatus.ReadyToTurnIn:
                    dialoguePanel.ShowQuest(
                        npc.Definition,
                        "你平安回来就好。森林里的魔物越来越不安分了。\n" +
                        "这份报酬收下吧，之后也许还有事情要拜托你。",
                        "汇报调查结果",
                        TurnInWorldQuest,
                        "稍后再说",
                        EndDialogue);
                    return true;
                case WorldQuestStatus.Active:
                    dialoguePanel.ShowQuest(
                        npc.Definition,
                        GetActiveQuestReminder(quest.ObjectiveIndex),
                        null,
                        null,
                        "告辞",
                        EndDialogue);
                    return true;
                case WorldQuestStatus.Completed:
                    dialoguePanel.ShowQuest(
                        npc.Definition,
                        "多亏了你的调查，村里能提前做好防备。路上多加小心。",
                        null,
                        null,
                        "告辞",
                        EndDialogue);
                    return true;
                default:
                    return false;
            }
        }

        private void AcceptWorldQuest()
        {
            if (!IsActive)
                return;

            bool accepted = WorldQuestRuntime.Instance?.TryAccept(
                RiverbendForestQuestRules.QuestId) == true;
            dialoguePanel?.ShowResponse(
                accepted
                    ? "带上这些金币和药品。先去河湾市场买一份粮食，再去低语森林调查。"
                    : "委托暂时无法登记，请稍后再试。");
        }

        private void TurnInWorldQuest()
        {
            if (!IsActive)
                return;

            bool completed = WorldQuestRuntime.Instance?.TryTurnIn(
                RiverbendForestQuestRules.QuestId,
                RiverbendForestQuestRules.GiverNpcId) == true;
            dialoguePanel?.ShowResponse(
                completed
                    ? "辛苦了。这份报酬是你应得的。"
                    : "调查记录还不完整，准备好后再来找我。");
        }

        private static string GetActiveQuestReminder(int objectiveIndex)
        {
            return objectiveIndex switch
            {
                RiverbendForestQuestRules.PurchaseObjectiveIndex =>
                    "先去河湾市场准备一份粮食，再出发去森林。",
                RiverbendForestQuestRules.EnterForestObjectiveIndex =>
                    "补给准备好了就去低语森林，注意路上的动静。",
                RiverbendForestQuestRules.DefeatSlimeObjectiveIndex =>
                    "低语森林就在村外。查清制造异响的魔物。",
                _ => "调查清楚后回来告诉我结果。"
            };
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
