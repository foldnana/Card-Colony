using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [SerializeField] private DialoguePanelView dialoguePanel;

        private CombatRect interactionRect;
        private CardInstance player;
        private CardInstance npc;
        private Vector3 playerReturnPosition;
        private Vector3 npcReturnPosition;
        private Vector3 playerDialoguePosition;
        private Vector3 npcDialoguePosition;
        private Tween playerFloatTween;
        private Tween npcFloatTween;
        private bool participantAnimationPositionsValid;

        public bool IsActive { get; private set; }
        public CardInstance Player => player;
        public CardInstance Npc => npc;
        public CombatRect InteractionRect => interactionRect;
        public bool HasActiveParticipantAnimation =>
            playerFloatTween != null && playerFloatTween.IsActive() &&
            npcFloatTween != null && npcFloatTween.IsActive();
        public IEnumerable<CardInstance> Participants
        {
            get
            {
                if (player != null)
                    yield return player;
                if (npc != null && npc != player)
                    yield return npc;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            dialoguePanel?.Hide();

            if (GameDirector.Instance != null)
                GameDirector.Instance.OnBeforeSave += HandleBeforeSave;
        }

        private void OnDestroy()
        {
            if (GameDirector.Instance != null)
                GameDirector.Instance.OnBeforeSave -= HandleBeforeSave;

            if (IsActive)
                EndDialogue();
            else
            {
                InputManager.Instance?.RemoveLock(this);
                npc?.GetComponent<LocationNpcActivity>()?.SetInteractionPaused(false);
            }

            if (Instance == this)
                Instance = null;
        }

        private void HandleBeforeSave(GameData gameData)
        {
            EndDialogue();
        }

        public static bool CanStartDialogue(CardInstance first, CardInstance second)
        {
            if (first == null || second == null || first == second)
                return false;

            return IsPlayerCharacter(first) && IsDialogueNpc(second) ||
                IsPlayerCharacter(second) && IsDialogueNpc(first);
        }

        public bool TryStartDialogueFromDrop(CardInstance droppedCard, float searchRadius)
        {
            if (IsActive || droppedCard == null || searchRadius <= 0f)
                return false;

            IEnumerable<CardInstance> nearbyCards = Physics
                .OverlapSphere(droppedCard.transform.position, searchRadius)
                .Select(hit => hit.GetComponent<CardInstance>())
                .Where(card => card != null && card != droppedCard)
                .Distinct()
                .OrderBy(card =>
                    (card.transform.position - droppedCard.transform.position).sqrMagnitude);

            CardInstance partner = nearbyCards.FirstOrDefault(card =>
                CanStartDialogue(droppedCard, card));
            return partner != null && StartDialogue(droppedCard, partner);
        }

        public bool StartDialogue(CardInstance first, CardInstance second)
        {
            if (IsActive ||
                dialoguePanel == null ||
                CombatManager.Instance == null ||
                !CanStartDialogue(first, second))
                return false;

            player = IsPlayerCharacter(first) ? first : second;
            npc = IsDialogueNpc(first) ? first : second;
            playerReturnPosition = DetachFromWorldStack(player);
            npcReturnPosition = DetachFromWorldStack(npc);

            interactionRect = CombatManager.Instance.CreateInteractionRect(
                new[] { player },
                new[] { npc });
            if (interactionRect == null)
            {
                RestoreParticipant(player, playerReturnPosition);
                RestoreParticipant(npc, npcReturnPosition);
                ClearParticipants();
                return false;
            }

            interactionRect.ConfigureInteractionTint(new Color(0.14f, 0.95f, 0.36f, 1f));

            IsActive = true;
            npc.GetComponent<LocationNpcActivity>()?.SetInteractionPaused(true);
            InputManager.Instance?.AddLock(this, allowCameraInput: true);
            StartParticipantAnimations();
            dialoguePanel.Show(npc.Definition, SelectReply, EndDialogue);
            return true;
        }

        public bool IsCardInDialogue(CardInstance card)
        {
            return IsActive && card != null && (card == player || card == npc);
        }

        public void SelectReply()
        {
            if (!IsActive || npc?.Definition == null)
                return;

            dialoguePanel?.ShowResponse(npc.Definition.DialogueResponseText);
        }

        public void EndDialogue()
        {
            if (!IsActive)
                return;

            IsActive = false;
            InputManager.Instance?.RemoveLock(this);
            dialoguePanel?.Hide();
            npc?.GetComponent<LocationNpcActivity>()?.SetInteractionPaused(false);
            StopParticipantAnimations();

            interactionRect?.Close();
            interactionRect = null;

            RestoreParticipant(player, playerReturnPosition);
            RestoreParticipant(npc, npcReturnPosition);
            CardManager.Instance?.ResolveOverlaps();
            ClearParticipants();
        }

        private static bool IsPlayerCharacter(CardInstance card)
        {
            return card?.Definition != null &&
                card.Definition.Category == CardCategory.Character &&
                card.Definition.Faction == CardFaction.Player;
        }

        private void StartParticipantAnimations()
        {
            StopParticipantAnimations();
            if (interactionRect == null || player == null || npc == null)
                return;

            playerDialoguePosition = interactionRect.GetLayoutPosition(player);
            npcDialoguePosition = interactionRect.GetLayoutPosition(npc);
            participantAnimationPositionsValid = true;
            playerFloatTween = CreateFloatTween(player, playerDialoguePosition, 0f);
            npcFloatTween = CreateFloatTween(npc, npcDialoguePosition, 0.18f);
        }

        private static Tween CreateFloatTween(
            CardInstance card,
            Vector3 basePosition,
            float delay)
        {
            return card.transform
                .DOMoveY(basePosition.y + 0.08f, 0.7f)
                .SetDelay(delay)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private void StopParticipantAnimations()
        {
            playerFloatTween?.Kill();
            npcFloatTween?.Kill();
            playerFloatTween = null;
            npcFloatTween = null;

            if (participantAnimationPositionsValid)
            {
                if (player != null)
                    player.SetTargetInstant(playerDialoguePosition, forceGround: true);
                if (npc != null)
                    npc.SetTargetInstant(npcDialoguePosition, forceGround: true);
            }
            participantAnimationPositionsValid = false;
        }

        private static bool IsDialogueNpc(CardInstance card)
        {
            return card?.Definition != null &&
                card.Definition.Category == CardCategory.Character &&
                card.Definition.Faction == CardFaction.Neutral &&
                card.Definition.DialogueEnabled;
        }

        private static Vector3 DetachFromWorldStack(CardInstance card)
        {
            if (card == null)
                return Vector3.zero;

            Vector3 position = card.Stack?.TargetPosition ?? card.transform.position;
            if (card.Stack != null)
            {
                if (card.Stack.IsCrafting)
                    CraftingManager.Instance?.StopCraftingTask(card.Stack);
                card.Stack.RemoveCard(card);
            }

            card.Stack = null;
            card.IsBeingDragged = false;
            return position.Flatten();
        }

        private static void RestoreParticipant(CardInstance card, Vector3 position)
        {
            if (card == null || card.Stack != null)
                return;

            var stack = new CardStack(card, position);
            CardManager.Instance?.RegisterStack(stack);
            Vector3 finalPosition = Board.Instance != null
                ? Board.Instance.EnforcePlacementRules(position, stack)
                : position;
            stack.SetTargetPosition(finalPosition);
        }

        private void ClearParticipants()
        {
            player = null;
            npc = null;
        }
    }
}
