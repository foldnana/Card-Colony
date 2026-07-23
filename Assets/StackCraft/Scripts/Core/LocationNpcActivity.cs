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
        private CardInstance card;
        private float wanderRadius;
        private float moveSpeed;
        private Vector2 idleRange;
        private float idleTimeRemaining;
        private bool configured;
        private bool interactionPaused;

        public LocationNpcActivityState State { get; private set; } = LocationNpcActivityState.Idle;
        public Vector3 HomePosition { get; private set; }
        public Vector3 Destination { get; private set; }
        public bool IsInteractionPaused => interactionPaused;

        private void Awake()
        {
            card = GetComponent<CardInstance>();
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
            HomePosition = homePosition.Flatten();
            Destination = HomePosition;
            wanderRadius = Mathf.Max(0.1f, maximumWanderRadius);
            moveSpeed = Mathf.Max(0.05f, movementSpeed);
            idleRange = new Vector2(
                Mathf.Max(0f, Mathf.Min(idleDurationRange.x, idleDurationRange.y)),
                Mathf.Max(0f, Mathf.Max(idleDurationRange.x, idleDurationRange.y)));
            configured = true;
            EnterIdle();
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
                    !checkedStacks.Add(otherStack) ||
                    otherCard.GetComponent<LocationNpcActivity>() == null)
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
            if (!CardPhysicsSolver.WouldOverlapAt(
                    card.Stack,
                    currentPosition,
                    otherStack,
                    0.04f))
                return false;

            Vector3 otherPosition = otherStack.TargetPosition.Flatten();
            float currentDistance = (
                currentPosition.Flatten() - otherPosition).sqrMagnitude;
            float nextDistance = (
                nextPosition.Flatten() - otherPosition).sqrMagnitude;
            return nextDistance > currentDistance + Mathf.Epsilon;
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
    }
}
