using UnityEngine;
using System.Collections.Generic;

namespace CryingSnow.StackCraft
{
    public enum LocationNpcActivityState
    {
        Idle,
        Moving
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CardInstance))]
    public sealed class LocationNpcActivity : MonoBehaviour
    {
        private static readonly HashSet<LocationNpcActivity>
            ActiveActivities = new();

        private CardInstance card;
        private float wanderRadius;
        private float moveSpeed;
        private Vector2 idleRange;
        private float idleTimeRemaining;
        private bool configured;
        private bool interactionPaused;

        [Header("Social Interaction")]
        [SerializeField, Min(0.5f)]
        private float socialSearchRadius = 2.5f;
        [SerializeField]
        private Vector2 socialCooldownRange = new(12f, 24f);
        [SerializeField, Min(0.5f)]
        private float socialConversationDuration = 4f;

        public LocationNpcActivityState State { get; private set; } = LocationNpcActivityState.Idle;
        public Vector3 HomePosition { get; private set; }
        public Vector3 Destination { get; private set; }
        public bool IsInteractionPaused => interactionPaused;
        public float SocialSearchRadius
        {
            get => socialSearchRadius;
            set => socialSearchRadius = Mathf.Max(0.5f, value);
        }
        public float SocialCooldownRemaining { get; private set; }

        private void Awake()
        {
            card = GetComponent<CardInstance>();
        }

        private void OnEnable()
        {
            ActiveActivities.Add(this);
        }

        private void OnDisable()
        {
            ActiveActivities.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveActivities.Remove(this);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Configure(
            CardInstance owner,
            Vector3 homePosition,
            float maximumWanderRadius,
            float movementSpeed,
            Vector2 idleDurationRange)
        {
            card = owner != null ? owner : GetComponent<CardInstance>();
            ActiveActivities.Add(this);
            HomePosition = homePosition.Flatten();
            Destination = HomePosition;
            wanderRadius = Mathf.Max(0.1f, maximumWanderRadius);
            moveSpeed = Mathf.Max(0.05f, movementSpeed);
            idleRange = new Vector2(
                Mathf.Max(0f, Mathf.Min(idleDurationRange.x, idleDurationRange.y)),
                Mathf.Max(0f, Mathf.Max(idleDurationRange.x, idleDurationRange.y)));
            configured = true;
            EnterIdle();
            ResetSocialCooldown();
        }

        public void SetDestination(Vector3 destination)
        {
            if (!configured)
                return;

            Vector3 offset = destination.Flatten() - HomePosition;
            if (offset.sqrMagnitude > wanderRadius * wanderRadius)
                offset = offset.normalized * wanderRadius;

            Destination = HomePosition + offset;
            if (Board.Instance != null && card?.Stack != null)
                Destination = Board.Instance.EnforcePlacementRules(Destination, card.Stack);

            State = Vector3.SqrMagnitude(CurrentPosition - Destination) <= 0.0001f
                ? LocationNpcActivityState.Idle
                : LocationNpcActivityState.Moving;
        }

        public void SetInteractionPaused(bool paused)
        {
            interactionPaused = paused;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f &&
                SocialCooldownRemaining > 0f)
            {
                SocialCooldownRemaining = Mathf.Max(
                    0f,
                    SocialCooldownRemaining - deltaTime);
            }

            if (!configured ||
                interactionPaused ||
                deltaTime <= 0f ||
                card == null ||
                card.Stack == null ||
                card.Stack.Cards.Count != 1 ||
                card.IsBeingDragged)
                return;

            if (State == LocationNpcActivityState.Idle)
            {
                if (SocialCooldownRemaining <= 0f &&
                    TryStartSocialConversation())
                {
                    return;
                }

                idleTimeRemaining -= deltaTime;
                if (idleTimeRemaining <= 0f)
                    ChooseWanderDestination();
                return;
            }

            Vector3 nextPosition = Vector3.MoveTowards(
                CurrentPosition,
                Destination,
                moveSpeed * deltaTime);
            if (IsNpcMovementBlocked(nextPosition))
            {
                Destination = CurrentPosition;
                EnterIdle();
                return;
            }

            card.Stack.SetTargetPosition(nextPosition, instant: true);

            if (Vector3.SqrMagnitude(nextPosition - Destination) <= 0.0001f)
            {
                EnterIdle();
                CardManager.Instance?.ResolveOverlaps();
            }
        }

        private Vector3 CurrentPosition => card?.Stack?.TargetPosition.Flatten() ?? transform.position.Flatten();

        private bool IsNpcMovementBlocked(Vector3 nextPosition)
        {
            if (card?.Stack == null || CardManager.Instance == null)
                return false;

            var checkedStacks = new HashSet<CardStack>();
            foreach (CardInstance otherCard in CardManager.Instance.AllCards)
            {
                CardStack otherStack = otherCard?.Stack;
                if (otherStack == null ||
                    otherStack == card.Stack ||
                    !checkedStacks.Add(otherStack))
                    continue;

                if (CardPhysicsSolver.WouldOverlapAt(
                        card.Stack,
                        nextPosition,
                        otherStack,
                        0.04f) &&
                    !IsSeparatingFromExistingOverlap(
                        nextPosition,
                        otherStack))
                    return true;
            }

            return false;
        }

        private bool IsSeparatingFromExistingOverlap(
            Vector3 nextPosition,
            CardStack otherStack)
        {
            Vector3 currentPosition = CurrentPosition;
            float currentOverlap =
                CardPhysicsSolver.GetPlanarOverlapAreaAt(
                    card.Stack,
                    currentPosition,
                    otherStack,
                    0.04f);
            if (currentOverlap <= 0f)
                return false;

            float nextOverlap =
                CardPhysicsSolver.GetPlanarOverlapAreaAt(
                    card.Stack,
                    nextPosition,
                    otherStack,
                    0.04f);
            return nextOverlap < currentOverlap - Mathf.Epsilon;
        }

        private void ChooseWanderDestination()
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            SetDestination(HomePosition + new Vector3(offset.x, 0f, offset.y));
        }

        private void EnterIdle()
        {
            State = LocationNpcActivityState.Idle;
            idleTimeRemaining = Random.Range(idleRange.x, idleRange.y);
        }

        private bool TryStartSocialConversation()
        {
            NpcInteractionManager interaction =
                NpcInteractionManager.Instance;
            if (interaction == null ||
                interaction.IsActive ||
                !IsSociallyAvailable)
            {
                return false;
            }

            float radiusSqr =
                socialSearchRadius * socialSearchRadius;
            LocationNpcActivity partner = null;
            float closestDistanceSqr = float.PositiveInfinity;
            foreach (LocationNpcActivity candidate in
                     ActiveActivities)
            {
                if (candidate == null ||
                    candidate == this ||
                    !candidate.IsSociallyAvailable)
                {
                    continue;
                }

                float distanceSqr =
                    (candidate.CurrentPosition -
                     CurrentPosition).sqrMagnitude;
                if (distanceSqr > radiusSqr ||
                    distanceSqr >= closestDistanceSqr)
                {
                    continue;
                }

                partner = candidate;
                closestDistanceSqr = distanceSqr;
            }
            if (partner == null ||
                !interaction.TryStartSocialInteraction(
                    card,
                    partner.card,
                    socialConversationDuration))
            {
                ResetSocialCooldown();
                return false;
            }

            ResetSocialCooldown();
            partner.ResetSocialCooldown();
            return true;
        }

        private bool IsSociallyAvailable =>
            configured &&
            !interactionPaused &&
            State == LocationNpcActivityState.Idle &&
            card?.Stack != null &&
            card.Stack.Cards.Count == 1 &&
            !card.Stack.IsLocked &&
            !card.Stack.IsCrafting &&
            !card.IsBeingDragged &&
            SocialCooldownRemaining <= 0f &&
            card.Definition != null &&
            card.Definition.Category == CardCategory.Character &&
            card.Definition.Faction == CardFaction.Neutral &&
            card.Definition.DialogueEnabled &&
            (card.Combatant == null ||
             !card.Combatant.IsInCombat);

        private void ResetSocialCooldown()
        {
            float minimum = Mathf.Max(
                0.5f,
                Mathf.Min(
                    socialCooldownRange.x,
                    socialCooldownRange.y));
            float maximum = Mathf.Max(
                minimum,
                Mathf.Max(
                    socialCooldownRange.x,
                    socialCooldownRange.y));
            SocialCooldownRemaining =
                Random.Range(minimum, maximum);
        }
    }
}
