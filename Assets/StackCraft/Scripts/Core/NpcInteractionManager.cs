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
        Approaching,
        ChoosingAction,
        Dialogue,
        Trade
    }

    [DisallowMultipleComponent]
    public sealed class NpcInteractionManager : MonoBehaviour
    {
        private const float ApproachDistance = 0.04f;
        private static readonly Color PlayerInteractionTint =
            new(0.14f, 0.95f, 0.36f, 1f);
        private static readonly Color SocialInteractionTint =
            new(0.35f, 0.72f, 1f, 1f);
        private static readonly Color ConflictInteractionTint =
            new(1f, 0.18f, 0.16f, 1f);

        public static NpcInteractionManager Instance { get; private set; }
        public static event Action<NpcInteractionManager> SessionChanged;
        public static event Action<NpcInteractionState> StateChanged;

        private CombatRect interactionRect;
        private CardInstance initiator;
        private CardInstance target;

        // Compatibility aliases retained for existing dialogue/trade callers.
        private CardInstance player;
        private CardInstance npc;

        private Vector3 initiatorReturnPosition;
        private Vector3 targetReturnPosition;
        private Vector3 initiatorInteractionPosition;
        private Vector3 targetInteractionPosition;
        private Tween initiatorFloatTween;
        private Tween targetFloatTween;
        private bool participantAnimationPositionsValid;
        private bool endingInteraction;
        private bool initialized;
        private bool autonomous;
        private bool conflict;
        private float autonomousConversationRemaining;

        public bool IsActive =>
            State != NpcInteractionState.None &&
            initiator != null &&
            target != null;
        private bool HasSessionArtifacts =>
            State != NpcInteractionState.None ||
            initiator != null ||
            target != null ||
            interactionRect != null;

        public NpcInteractionState State { get; private set; }
        public CardInstance Initiator => initiator;
        public CardInstance Target => target;
        public CardInstance Player => player;
        public CardInstance Npc => npc;
        public bool IsPlayerInvolved => player != null;
        public bool IsAutonomous => autonomous;
        public bool IsConflict => conflict;
        public CombatRect InteractionRect => interactionRect;
        public bool HasActiveParticipantAnimation =>
            initiatorFloatTween != null &&
            initiatorFloatTween.IsActive() &&
            targetFloatTween != null &&
            targetFloatTween.IsActive();

        public IEnumerable<CardInstance> Participants
        {
            get
            {
                if (initiator != null)
                    yield return initiator;
                if (target != null && target != initiator)
                    yield return target;
            }
        }

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        public void Tick(float deltaTime)
        {
            if (HasSessionArtifacts &&
                (initiator == null ||
                 target == null ||
                 interactionRect == null))
            {
                EndInteraction();
                return;
            }

            if (!IsActive)
                return;

            if (State == NpcInteractionState.Approaching)
            {
                Vector3 destination =
                    interactionRect != null
                        ? interactionRect.GetLayoutPosition(initiator)
                        : initiator.transform.position;
                if ((initiator.transform.position - destination)
                    .sqrMagnitude <=
                    ApproachDistance * ApproachDistance)
                {
                    initiator.SetTargetInstant(
                        destination,
                        forceGround: true);
                    StartParticipantAnimations();
                    SetState(
                        autonomous
                            ? NpcInteractionState.Dialogue
                            : NpcInteractionState.ChoosingAction);
                    return;
                }
            }

            if (autonomous && !conflict &&
                State == NpcInteractionState.Dialogue)
            {
                autonomousConversationRemaining -=
                    Mathf.Max(0f, deltaTime);
                if (autonomousConversationRemaining <= 0f)
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
                Instance.IsPlayerInvolved &&
                Instance.initiator == candidatePlayer &&
                Instance.target == candidateNpc)
            {
                return true;
            }

            return IsAvailableParticipant(candidatePlayer) &&
                IsAvailableParticipant(candidateNpc);
        }

        public static bool CanStartSocialInteraction(
            CardInstance first,
            CardInstance second)
        {
            return first != null &&
                second != null &&
                first != second &&
                IsNeutralNpc(first) &&
                IsNeutralNpc(second) &&
                first.Definition.DialogueEnabled &&
                second.Definition.DialogueEnabled &&
                IsAvailableParticipant(first) &&
                IsAvailableParticipant(second);
        }

        public static bool CanStartConflictInteraction(
            CardInstance first,
            CardInstance second)
        {
            return first != null &&
                second != null &&
                first != second &&
                first.Combatant != null &&
                second.Combatant != null &&
                IsAvailableParticipant(first) &&
                IsAvailableParticipant(second);
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
            if (!CanStartInteraction(first, second))
                return false;

            CardInstance nextPlayer =
                IsPlayerCharacter(first) ? first : second;
            CardInstance nextNpc =
                IsNeutralNpc(first) ? first : second;
            if (IsActive &&
                IsPlayerInvolved &&
                initiator == nextPlayer &&
                target == nextNpc)
            {
                if (State != NpcInteractionState.Approaching)
                    ShowActions();
                return true;
            }

            if (IsActive)
                EndInteraction();

            return StartSession(
                nextPlayer,
                nextNpc,
                isAutonomous: false,
                conversationDuration: 0f,
                isConflict: false,
                animateApproach: true);
        }

        public bool TryStartSocialInteraction(
            CardInstance socialInitiator,
            CardInstance socialTarget,
            float conversationDuration)
        {
            if (IsActive ||
                !CanStartSocialInteraction(
                    socialInitiator,
                    socialTarget))
            {
                return false;
            }

            return StartSession(
                socialInitiator,
                socialTarget,
                isAutonomous: true,
                conversationDuration:
                    Mathf.Max(0.5f, conversationDuration),
                isConflict: false,
                animateApproach: true);
        }

        public bool TryStartConflictInteraction(
            CardInstance conflictInitiator,
            CardInstance conflictTarget)
        {
            if (IsActive || !CanStartConflictInteraction(
                    conflictInitiator, conflictTarget))
            {
                return false;
            }

            return StartSession(
                conflictInitiator,
                conflictTarget,
                isAutonomous: true,
                conversationDuration: 0f,
                isConflict: true,
                animateApproach: false);
        }

        private bool StartSession(
            CardInstance nextInitiator,
            CardInstance nextTarget,
            bool isAutonomous,
            float conversationDuration,
            bool isConflict,
            bool animateApproach)
        {
            if (CombatManager.Instance == null)
                return false;

            initiator = nextInitiator;
            target = nextTarget;
            autonomous = isAutonomous;
            conflict = isConflict;
            autonomousConversationRemaining = conversationDuration;
            player = isAutonomous ? null : nextInitiator;
            npc = nextTarget;

            initiatorReturnPosition =
                DetachFromWorldStack(initiator);
            targetReturnPosition =
                DetachFromWorldStack(target);

            interactionRect =
                CombatManager.Instance.CreateAnchoredInteractionRect(
                    new[] { initiator },
                    new[] { target },
                    targetReturnPosition,
                    animateApproach);
            if (interactionRect == null)
            {
                RestoreParticipant(
                    initiator,
                    initiatorReturnPosition);
                RestoreParticipant(
                    target,
                    targetReturnPosition);
                ClearParticipants();
                return false;
            }

            interactionRect.ConfigureInteractionTint(
                conflict
                    ? ConflictInteractionTint
                    : autonomous
                    ? SocialInteractionTint
                    : PlayerInteractionTint);
            SetActivityPaused(initiator, true);
            SetActivityPaused(target, true);

            if (!autonomous)
            {
                InputManager.Instance?.AddLock(
                    this,
                    allowCameraInput: true);
                target.GetComponent<NpcTrader>()
                    ?.SelectForInteraction();
            }

            SetState(animateApproach
                ? NpcInteractionState.Approaching
                : NpcInteractionState.Dialogue);
            if (!autonomous)
                SessionChanged?.Invoke(this);
            return true;
        }

        public bool BeginDialogue(out string reason)
        {
            reason = string.Empty;
            if (!IsActive ||
                !IsPlayerInvolved ||
                State != NpcInteractionState.ChoosingAction)
            {
                reason = "请先等待人物靠近并开始互动。";
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
            if (!IsActive ||
                !IsPlayerInvolved ||
                State != NpcInteractionState.ChoosingAction)
            {
                reason = "请先等待人物靠近并开始互动。";
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
            if (!IsActive ||
                !IsPlayerInvolved ||
                State == NpcInteractionState.Approaching)
            {
                return;
            }

            if (DialogueManager.Instance?.IsActive == true)
                DialogueManager.Instance.EndDialogue();
            else
                SetState(NpcInteractionState.ChoosingAction);
        }

        public void HandleDialogueEnded()
        {
            if (IsActive &&
                IsPlayerInvolved &&
                !endingInteraction)
            {
                SetState(NpcInteractionState.ChoosingAction);
            }
        }

        public void EndInteraction()
        {
            if (!HasSessionArtifacts || endingInteraction)
                return;

            endingInteraction = true;
            bool endedAutonomousSession = autonomous;
            DialogueManager.Instance
                ?.EndDialogueForInteractionEnd();
            InputManager.Instance?.RemoveLock(this);
            SetActivityPaused(initiator, false);
            SetActivityPaused(target, false);
            StopParticipantAnimations();

            if (interactionRect != null)
                interactionRect.Close();
            interactionRect = null;

            RestoreParticipant(
                initiator,
                initiatorReturnPosition);
            RestoreParticipant(
                target,
                targetReturnPosition);
            CardManager.Instance?.ResolveOverlaps();
            ClearParticipants();
            SetState(NpcInteractionState.None);
            endingInteraction = false;
            if (!endedAutonomousSession)
                SessionChanged?.Invoke(null);
        }

        public void EndInteractionForCombatTransition(
            IEnumerable<CardInstance> retainedParticipants,
            float duration = 0.22f)
        {
            if (!HasSessionArtifacts || endingInteraction)
                return;

            var retained = new HashSet<CardInstance>(
                retainedParticipants?.Where(card => card != null) ??
                Enumerable.Empty<CardInstance>());
            endingInteraction = true;
            bool endedAutonomousSession = autonomous;
            DialogueManager.Instance?.EndDialogueForInteractionEnd();
            InputManager.Instance?.RemoveLock(this);
            SetActivityPaused(initiator, false);
            SetActivityPaused(target, false);
            StopParticipantAnimations();

            interactionRect?.CloseAnimated(duration);
            interactionRect = null;
            if (!retained.Contains(initiator))
                RestoreParticipant(initiator, initiatorReturnPosition);
            if (!retained.Contains(target))
                RestoreParticipant(target, targetReturnPosition);

            ClearParticipants();
            SetState(NpcInteractionState.None);
            endingInteraction = false;
            if (!endedAutonomousSession)
                SessionChanged?.Invoke(null);
        }

        public bool TryGetReturnPosition(
            CardInstance participant,
            out Vector3 position)
        {
            if (HasSessionArtifacts && participant != null)
            {
                if (participant == initiator)
                {
                    position = initiatorReturnPosition;
                    return true;
                }
                if (participant == target)
                {
                    position = targetReturnPosition;
                    return true;
                }
            }
            position = default;
            return false;
        }

        public bool IsCardInInteraction(CardInstance card)
        {
            return IsActive &&
                card != null &&
                (card == initiator || card == target);
        }

        public bool CanWithdrawPlayerCard(CardInstance card)
        {
            return IsActive &&
                IsPlayerInvolved &&
                !autonomous &&
                card != null &&
                card == player &&
                card == initiator &&
                !card.IsDowned &&
                (card.Definition == null ||
                 card.Definition.PlayerDraggable);
        }

        public bool TryWithdrawPlayerCard(CardInstance card)
        {
            if (!CanWithdrawPlayerCard(card))
                return false;

            Vector3 withdrawalPosition =
                card.transform.position.Flatten();
            initiatorReturnPosition = withdrawalPosition;
            EndInteraction();

            if (card.Stack == null)
                return false;

            card.Stack.SetTargetPosition(
                withdrawalPosition,
                instant: true);
            return true;
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
                initiator == null ||
                target == null)
            {
                return;
            }

            initiatorInteractionPosition =
                interactionRect.GetLayoutPosition(initiator);
            targetInteractionPosition =
                interactionRect.GetLayoutPosition(target);
            participantAnimationPositionsValid = true;
            initiatorFloatTween = CreateFloatTween(
                initiator,
                initiatorInteractionPosition,
                0f);
            targetFloatTween = CreateFloatTween(
                target,
                targetInteractionPosition,
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
            initiatorFloatTween?.Kill();
            targetFloatTween?.Kill();
            initiatorFloatTween = null;
            targetFloatTween = null;

            if (participantAnimationPositionsValid)
            {
                if (initiator != null)
                {
                    initiator.SetTargetInstant(
                        initiatorInteractionPosition,
                        forceGround: true);
                }
                if (target != null)
                {
                    target.SetTargetInstant(
                        targetInteractionPosition,
                        forceGround: true);
                }
            }
            participantAnimationPositionsValid = false;
        }

        private static void SetActivityPaused(
            CardInstance card,
            bool paused)
        {
            if (card == null)
                return;

            card.GetComponent<LocationNpcActivity>()
                ?.SetInteractionPaused(paused);
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
                (card.Combatant == null ||
                 !card.Combatant.IsInCombat);
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
                ? Board.Instance.EnforcePlacementRules(
                    position,
                    stack)
                : position;
            stack.SetTargetPosition(finalPosition);
        }

        private void ClearParticipants()
        {
            initiator = null;
            target = null;
            player = null;
            npc = null;
            autonomous = false;
            conflict = false;
            autonomousConversationRemaining = 0f;
        }
    }
}
