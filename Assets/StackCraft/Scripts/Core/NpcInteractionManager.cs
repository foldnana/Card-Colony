using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    public enum NpcInteractionState
    {
        None,
        ChoosingAction,
        Dialogue,
        Trade
    }

    [DisallowMultipleComponent]
    public sealed class NpcInteractionManager : MonoBehaviour
    {
        public static NpcInteractionManager Instance { get; private set; }
        public static event Action<NpcInteractionManager> SessionChanged;
        public static event Action<NpcInteractionState> StateChanged;

        private CombatRect interactionRect;
        private CardInstance player;
        private CardInstance npc;
        private Vector3 playerReturnPosition;
        private Vector3 npcReturnPosition;
        private Vector3 playerInteractionPosition;
        private Vector3 npcInteractionPosition;
        private Tween playerFloatTween;
        private Tween npcFloatTween;
        private bool participantAnimationPositionsValid;
        private bool endingInteraction;
        private bool initialized;

        public bool IsActive =>
            State != NpcInteractionState.None &&
            player != null &&
            npc != null;
        private bool HasSessionArtifacts =>
            State != NpcInteractionState.None ||
            player != null ||
            npc != null ||
            interactionRect != null;
        public NpcInteractionState State { get; private set; }
        public CardInstance Player => player;
        public CardInstance Npc => npc;
        public CombatRect InteractionRect => interactionRect;
        public bool HasActiveParticipantAnimation =>
            playerFloatTween != null &&
            playerFloatTween.IsActive() &&
            npcFloatTween != null &&
            npcFloatTween.IsActive();
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
            Initialize();
        }

        private void Update()
        {
            if (HasSessionArtifacts &&
                (player == null || npc == null))
            {
                EndInteraction();
            }
        }

        private void Initialize()
        {
            if (initialized)
                return;
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            initialized = true;
            if (GameDirector.Instance != null)
                GameDirector.Instance.OnBeforeSave += HandleBeforeSave;
        }

        private void OnDestroy()
        {
            if (GameDirector.Instance != null)
                GameDirector.Instance.OnBeforeSave -= HandleBeforeSave;

            if (HasSessionArtifacts)
                EndInteraction();
            else
                InputManager.Instance?.RemoveLock(this);

            if (Instance == this)
                Instance = null;
        }

        private void HandleBeforeSave(GameData gameData)
        {
            EndInteraction();
        }

        public static NpcInteractionManager Ensure(GameObject host)
        {
            if (Instance != null)
                return Instance;
            if (host == null)
                return null;

            NpcInteractionManager manager =
                host.GetComponent<NpcInteractionManager>();
            if (manager == null)
                manager = host.AddComponent<NpcInteractionManager>();
            manager.Initialize();
            return manager;
        }

        public static bool CanStartInteraction(
            CardInstance first,
            CardInstance second)
        {
            if (first == null || second == null || first == second)
                return false;

            CardInstance candidatePlayer =
                IsPlayerCharacter(first) ? first :
                IsPlayerCharacter(second) ? second : null;
            CardInstance candidateNpc =
                IsNeutralNpc(first) ? first :
                IsNeutralNpc(second) ? second : null;
            if (candidatePlayer == null || candidateNpc == null)
                return false;

            if (Instance?.IsActive == true &&
                Instance.player == candidatePlayer &&
                Instance.npc == candidateNpc)
            {
                return true;
            }

            return IsAvailableParticipant(candidatePlayer) &&
                IsAvailableParticipant(candidateNpc);
        }

        public bool TryStartInteractionFromDrop(
            CardInstance droppedCard,
            float searchRadius)
        {
            if (droppedCard == null || searchRadius <= 0f)
                return false;

            IEnumerable<CardInstance> nearbyCards = Physics
                .OverlapSphere(
                    droppedCard.transform.position,
                    searchRadius)
                .Select(hit => hit.GetComponent<CardInstance>())
                .Where(card => card != null && card != droppedCard)
                .Distinct()
                .OrderBy(card =>
                    (card.transform.position -
                     droppedCard.transform.position).sqrMagnitude);

            CardInstance partner = nearbyCards.FirstOrDefault(card =>
                CanStartInteraction(droppedCard, card));
            return partner != null &&
                StartInteraction(droppedCard, partner);
        }

        public bool StartInteraction(
            CardInstance first,
            CardInstance second)
        {
            if (!CanStartInteraction(first, second) ||
                CombatManager.Instance == null)
            {
                return false;
            }

            CardInstance nextPlayer =
                IsPlayerCharacter(first) ? first : second;
            CardInstance nextNpc =
                IsNeutralNpc(first) ? first : second;
            if (IsActive &&
                player == nextPlayer &&
                npc == nextNpc)
            {
                ShowActions();
                return true;
            }

            if (IsActive)
                EndInteraction();

            player = nextPlayer;
            npc = nextNpc;
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

            interactionRect.ConfigureInteractionTint(
                new Color(0.14f, 0.95f, 0.36f, 1f));
            npc.GetComponent<LocationNpcActivity>()
                ?.SetInteractionPaused(true);
            InputManager.Instance?.AddLock(
                this,
                allowCameraInput: true);
            StartParticipantAnimations();

            npc.GetComponent<NpcTrader>()?.SelectForInteraction();
            SetState(NpcInteractionState.ChoosingAction);
            SessionChanged?.Invoke(this);
            return true;
        }

        public bool BeginDialogue(out string reason)
        {
            reason = string.Empty;
            if (!IsActive)
            {
                reason = "请先开始人物互动。";
                return false;
            }
            if (!DialogueManager.CanStartDialogue(player, npc))
            {
                reason = "这个人物暂时没有可用的对话。";
                return false;
            }
            if (DialogueManager.Instance == null ||
                !DialogueManager.Instance.StartDialogueInSession(
                    player,
                    npc))
            {
                reason = "现在无法开始交谈。";
                return false;
            }

            SetState(NpcInteractionState.Dialogue);
            return true;
        }

        public bool BeginTrade(out string reason)
        {
            reason = string.Empty;
            if (!IsActive)
            {
                reason = "请先开始人物互动。";
                return false;
            }

            NpcTrader trader = npc.GetComponent<NpcTrader>();
            if (trader == null ||
                !NpcTradeService.CanTradeNow(trader, out reason))
            {
                if (string.IsNullOrWhiteSpace(reason))
                    reason = "这个人物当前不能交易。";
                return false;
            }

            trader.SelectForInteraction();
            SetState(NpcInteractionState.Trade);
            return true;
        }

        public void ShowActions()
        {
            if (!IsActive)
                return;

            if (DialogueManager.Instance?.IsActive == true)
                DialogueManager.Instance.EndDialogue();
            else
                SetState(NpcInteractionState.ChoosingAction);
        }

        public void HandleDialogueEnded()
        {
            if (IsActive && !endingInteraction)
                SetState(NpcInteractionState.ChoosingAction);
        }

        public void EndInteraction()
        {
            if (!HasSessionArtifacts || endingInteraction)
                return;

            endingInteraction = true;
            DialogueManager.Instance?.EndDialogueForInteractionEnd();
            InputManager.Instance?.RemoveLock(this);
            if (npc != null)
            {
                npc.GetComponent<LocationNpcActivity>()
                    ?.SetInteractionPaused(false);
            }
            StopParticipantAnimations();

            if (interactionRect != null)
                interactionRect.Close();
            interactionRect = null;

            RestoreParticipant(player, playerReturnPosition);
            RestoreParticipant(npc, npcReturnPosition);
            CardManager.Instance?.ResolveOverlaps();
            ClearParticipants();
            SetState(NpcInteractionState.None);
            endingInteraction = false;
            SessionChanged?.Invoke(null);
        }

        public bool IsCardInInteraction(CardInstance card)
        {
            return IsActive &&
                card != null &&
                (card == player || card == npc);
        }

        private void SetState(NpcInteractionState state)
        {
            if (State == state)
                return;

            State = state;
            StateChanged?.Invoke(state);
        }

        private void StartParticipantAnimations()
        {
            StopParticipantAnimations();
            if (interactionRect == null ||
                player == null ||
                npc == null)
            {
                return;
            }

            playerInteractionPosition =
                interactionRect.GetLayoutPosition(player);
            npcInteractionPosition =
                interactionRect.GetLayoutPosition(npc);
            participantAnimationPositionsValid = true;
            playerFloatTween = CreateFloatTween(
                player,
                playerInteractionPosition,
                0f);
            npcFloatTween = CreateFloatTween(
                npc,
                npcInteractionPosition,
                0.18f);
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
                {
                    player.SetTargetInstant(
                        playerInteractionPosition,
                        forceGround: true);
                }
                if (npc != null)
                {
                    npc.SetTargetInstant(
                        npcInteractionPosition,
                        forceGround: true);
                }
            }
            participantAnimationPositionsValid = false;
        }

        private static bool IsPlayerCharacter(CardInstance card)
        {
            return card?.Definition != null &&
                card.Definition.Category == CardCategory.Character &&
                card.Definition.Faction == CardFaction.Player;
        }

        private static bool IsNeutralNpc(CardInstance card)
        {
            return card?.Definition != null &&
                card.Definition.Category == CardCategory.Character &&
                card.Definition.Faction == CardFaction.Neutral;
        }

        private static bool IsAvailableParticipant(CardInstance card)
        {
            return card != null &&
                card.Stack != null &&
                !card.Stack.IsLocked &&
                !card.Stack.IsCrafting &&
                (card.Combatant == null || !card.Combatant.IsInCombat);
        }

        private static Vector3 DetachFromWorldStack(CardInstance card)
        {
            if (card == null)
                return Vector3.zero;

            Vector3 position =
                card.Stack?.TargetPosition ??
                card.transform.position;
            if (card.Stack != null)
            {
                if (card.Stack.IsCrafting)
                {
                    CraftingManager.Instance
                        ?.StopCraftingTask(card.Stack);
                }
                card.Stack.RemoveCard(card);
            }

            card.Stack = null;
            card.IsBeingDragged = false;
            return position.Flatten();
        }

        private static void RestoreParticipant(
            CardInstance card,
            Vector3 position)
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
