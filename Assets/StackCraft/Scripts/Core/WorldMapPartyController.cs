using UnityEngine;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class WorldMapPartyController : MonoBehaviour, ICardDropHandler, ICardDragStartHandler
    {
        private WorldMapBootstrap worldMap;
        private CardInstance partyCard;
        private float destinationSnapRadius;
        private float configuredTravelDuration;
        private int travelOriginIndex = -1;
        private int travelDestinationIndex = -1;
        private float travelElapsed;
        private float activeTravelDuration;
        private ProgressUI travelProgressUI;

        public int CurrentLocationIndex { get; private set; } = -1;
        public bool IsTraveling { get; private set; }
        public CardInstance PartyCard => partyCard;
        public float TravelProgress => IsTraveling && activeTravelDuration > 0f
            ? Mathf.Clamp01(travelElapsed / activeTravelDuration)
            : 0f;
        public ProgressUI TravelProgressUI => travelProgressUI;

        private void Update()
        {
            if (IsTraveling)
                TickTravel(Time.deltaTime);
        }

        private void OnDisable()
        {
            if (IsTraveling)
                CancelTravel(returnToOrigin: true, instant: true);
        }

        private void OnDestroy()
        {
            CancelTravel(returnToOrigin: false, instant: true);

            if (worldMap != null && partyCard != null)
                worldMap.DetachPartyFromLocation(partyCard);
        }

        public void Initialize(
            WorldMapBootstrap map,
            CardInstance card,
            float snapRadius,
            float travelDuration)
        {
            worldMap = map;
            partyCard = card;
            destinationSnapRadius = Mathf.Max(0.1f, snapRadius);
            configuredTravelDuration = Mathf.Max(0.1f, travelDuration);

            if (partyCard?.Stack == null || worldMap == null)
                return;

            partyCard.Stack.IsLocked = false;
            CurrentLocationIndex = worldMap.FindNearestLocationIndex(
                partyCard.Stack.TargetPosition,
                float.PositiveInfinity);

            if (CurrentLocationIndex >= 0)
                DockAt(CurrentLocationIndex, instant: true);
        }

        public bool HandleDrop(CardInstance card, Vector3 dropPosition)
        {
            if (card != partyCard)
                return false;

            if (worldMap == null || partyCard.Stack == null)
                return true;

            if (IsTraveling)
                return true;

            int destinationIndex = worldMap.FindNearestLocationIndex(
                dropPosition,
                destinationSnapRadius);

            if (destinationIndex < 0)
            {
                ReturnToCurrentLocation("请把小队放到相邻的地点卡上");
                return true;
            }

            if (CurrentLocationIndex < 0)
            {
                CurrentLocationIndex = worldMap.FindNearestLocationIndex(
                    partyCard.Stack.TargetPosition,
                    float.PositiveInfinity);
            }

            if (destinationIndex == CurrentLocationIndex)
            {
                DockAt(CurrentLocationIndex, instant: false);
                worldMap.NotifyPartyStateChanged(this, "驻扎中");
                return true;
            }

            if (!worldMap.AreLocationsConnected(CurrentLocationIndex, destinationIndex))
            {
                ReturnToCurrentLocation("两地之间没有可通行的路线");
                return true;
            }

            BeginTravel(destinationIndex);
            return true;
        }

        public bool TryTravelToLocation(int destinationIndex)
        {
            if (worldMap == null || partyCard?.Stack == null || IsTraveling)
                return false;

            if (CurrentLocationIndex < 0 || destinationIndex == CurrentLocationIndex)
                return false;

            if (!worldMap.AreLocationsConnected(CurrentLocationIndex, destinationIndex) ||
                !worldMap.IsRuntimeLocationAvailable(destinationIndex))
            {
                return false;
            }

            BeginTravel(destinationIndex);
            return true;
        }

        public void HandleDragStarted(CardInstance card)
        {
            if (card != partyCard || worldMap == null || IsTraveling)
                return;

            worldMap.DetachPartyFromLocation(partyCard);
        }

        private void BeginTravel(int destinationIndex)
        {
            travelOriginIndex = CurrentLocationIndex;
            travelDestinationIndex = destinationIndex;
            travelElapsed = 0f;
            activeTravelDuration = worldMap.GetTravelDuration(
                travelOriginIndex,
                travelDestinationIndex);
            if (activeTravelDuration <= 0f)
                activeTravelDuration = configuredTravelDuration;

            IsTraveling = true;
            partyCard.Stack.IsLocked = true;
            worldMap.DetachPartyFromLocation(partyCard);
            worldMap.SetTravelHighlights(
                travelOriginIndex,
                travelDestinationIndex,
                highlighted: true);

            Vector3 stackPosition = worldMap.GetTravelStackPosition(
                travelDestinationIndex,
                partyCard);
            partyCard.Stack.SetTargetPosition(stackPosition, instant: true);
            travelProgressUI = worldMap.CreateTravelProgressUI(stackPosition);
            travelProgressUI?.UpdateProgress(stackPosition, 0f);

            worldMap.NotifyPartyStateChanged(
                this,
                $"前往 {worldMap.GetLocationName(destinationIndex)}");
        }

        private void TickTravel(float deltaTime)
        {
            if (!IsTraveling)
                return;

            if (worldMap == null || partyCard?.Stack == null)
            {
                CancelTravel(returnToOrigin: false, instant: true);
                return;
            }

            bool originAvailable = worldMap.IsRuntimeLocationAvailable(travelOriginIndex);
            if (!originAvailable ||
                !worldMap.IsRuntimeLocationAvailable(travelDestinationIndex))
            {
                CancelTravel(returnToOrigin: originAvailable, instant: true);
                return;
            }

            travelElapsed = Mathf.Min(
                activeTravelDuration,
                travelElapsed + Mathf.Max(0f, deltaTime));

            Vector3 stackPosition = worldMap.GetTravelStackPosition(
                travelDestinationIndex,
                partyCard);
            partyCard.Stack.SetTargetPosition(stackPosition, instant: true);
            travelProgressUI?.UpdateProgress(stackPosition, TravelProgress);

            if (travelElapsed >= activeTravelDuration)
                CompleteTravel();
        }

        private void CompleteTravel()
        {
            int completedDestination = travelDestinationIndex;
            ClearTravelPresentation(instant: false);

            CurrentLocationIndex = completedDestination;
            DockAt(CurrentLocationIndex, instant: true);
            partyCard.Stack.IsLocked = false;
            IsTraveling = false;
            worldMap.NotifyPartyStateChanged(this, "驻扎中");
        }

        private void CancelTravel(bool returnToOrigin, bool instant)
        {
            int canceledOrigin = travelOriginIndex;
            ClearTravelPresentation(instant);
            IsTraveling = false;

            if (partyCard?.Stack != null)
                partyCard.Stack.IsLocked = false;

            if (!returnToOrigin || worldMap == null || canceledOrigin < 0)
                return;

            CurrentLocationIndex = canceledOrigin;
            DockAt(canceledOrigin, instant: true);
            worldMap.NotifyPartyStateChanged(this, "驻扎中");
        }

        private void ClearTravelPresentation(bool instant)
        {
            if (worldMap != null)
            {
                worldMap.SetTravelHighlights(
                    travelOriginIndex,
                    travelDestinationIndex,
                    highlighted: false,
                    instant: instant);
                worldMap.ReleaseTravelProgressUI(travelProgressUI);
            }
            else if (travelProgressUI != null)
            {
                if (Application.isPlaying)
                    Destroy(travelProgressUI.gameObject);
                else
                    DestroyImmediate(travelProgressUI.gameObject);
            }

            travelProgressUI = null;
            travelOriginIndex = -1;
            travelDestinationIndex = -1;
            travelElapsed = 0f;
            activeTravelDuration = 0f;
        }

        private void ReturnToCurrentLocation(string message)
        {
            if (CurrentLocationIndex >= 0)
                DockAt(CurrentLocationIndex, instant: false);

            worldMap.NotifyPartyStateChanged(this, message);
        }

        private void DockAt(int locationIndex, bool instant)
        {
            if (partyCard?.Stack == null || worldMap == null)
                return;

            CurrentLocationIndex = locationIndex;
            worldMap.DockPartyAtLocation(locationIndex, partyCard, instant);
        }
    }
}
