using System.Collections;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class WorldMapPartyController : MonoBehaviour, ICardDropHandler, ICardDragStartHandler
    {
        private WorldMapBootstrap worldMap;
        private CardInstance partyCard;
        private float destinationSnapRadius;
        private float travelSpeed;

        public int CurrentLocationIndex { get; private set; } = -1;
        public bool IsTraveling { get; private set; }
        public CardInstance PartyCard => partyCard;

        private void OnDestroy()
        {
            if (worldMap != null && partyCard != null)
                worldMap.DetachPartyFromLocation(partyCard);
        }

        public void Initialize(
            WorldMapBootstrap map,
            CardInstance card,
            float snapRadius,
            float speed)
        {
            worldMap = map;
            partyCard = card;
            destinationSnapRadius = Mathf.Max(0.1f, snapRadius);
            travelSpeed = Mathf.Max(0.1f, speed);

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

            StartCoroutine(TravelTo(destinationIndex));
            return true;
        }

        public void HandleDragStarted(CardInstance card)
        {
            if (card != partyCard || worldMap == null || IsTraveling)
                return;

            worldMap.DetachPartyFromLocation(partyCard);
        }

        private IEnumerator TravelTo(int destinationIndex)
        {
            IsTraveling = true;
            partyCard.Stack.IsLocked = true;

            Vector3 start = worldMap.GetPartyDockPosition(CurrentLocationIndex);
            Vector3 destination = worldMap.GetPartyDockPosition(destinationIndex);
            worldMap.DetachPartyFromLocation(partyCard);
            partyCard.Stack.SetTargetPosition(start, instant: true);
            worldMap.NotifyPartyStateChanged(
                this,
                $"前往 {worldMap.GetLocationName(destinationIndex)}");

            float duration = Mathf.Max(0.1f, Vector3.Distance(start, destination) / travelSpeed);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                partyCard.Stack.SetTargetPosition(
                    Vector3.Lerp(start, destination, Mathf.SmoothStep(0f, 1f, progress)),
                    instant: true);
                yield return null;
            }

            CurrentLocationIndex = destinationIndex;
            DockAt(CurrentLocationIndex, instant: true);
            partyCard.Stack.IsLocked = false;
            IsTraveling = false;
            worldMap.NotifyPartyStateChanged(this, "驻扎中");
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
