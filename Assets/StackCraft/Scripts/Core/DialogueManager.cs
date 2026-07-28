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
            dialoguePanel.Show(
                npc.Definition,
                SelectReply,
                EndDialogue);
            return true;
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
