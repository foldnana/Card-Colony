using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class LocationEntrance : MonoBehaviour, IOnStackable, IPointerClickHandler
    {
        private static LocationEntrance activeSelection;

        public static event Action<LocationEntrance> SelectionChanged;
        public static LocationEntrance ActiveSelection => activeSelection;

        [Header("Person Slot")]
        [SerializeField] private Vector3 personSlotOffset = new(0f, 0.01f, -0.55f);
        [SerializeField, Range(0.5f, 1f)] private float personSlotScale = 0.78f;
        [SerializeField] private Color occupiedOutlineColor =
            new(0.95f, 0.72f, 0.22f, 1f);

        private CardInstance buildingCard;
        private WorldMapPersonSlot personSlot;

        public string DestinationLocationId { get; private set; }
        public CardInstance Card => buildingCard;
        public CardInstance Occupant => personSlot != null ? personSlot.Occupant : null;
        public bool CanEnter => Occupant != null &&
            !string.IsNullOrWhiteSpace(DestinationLocationId);
        public bool IsSelected { get; private set; }

        public void Configure(string destinationLocationId)
        {
            DestinationLocationId = destinationLocationId;
            EnsurePersonSlot();
        }

        public bool OnStack(CardStack droppedStack)
        {
            if (!CanAccept(droppedStack) || Occupant != null ||
                string.IsNullOrWhiteSpace(DestinationLocationId))
                return false;

            EnsurePersonSlot();
            CardInstance person = droppedStack.Cards.Single();
            personSlot.Attach(person, personSlotOffset, personSlotScale, instant: false);

            LocationEntranceOccupant dragHandler =
                person.GetComponent<LocationEntranceOccupant>();
            if (dragHandler == null)
                dragHandler = person.gameObject.AddComponent<LocationEntranceOccupant>();
            dragHandler.Configure(this);

            if (IsSelected)
                personSlot.ShowCards();

            RefreshOutline();
            if (IsSelected)
                SelectionChanged?.Invoke(this);
            return true;
        }

        public bool TryEnter()
        {
            if (!CanEnter || GameDirector.Instance == null)
                return false;

            return GameDirector.Instance.EnterLocation(
                DestinationLocationId,
                new[] { new CardData(Occupant) });
        }

        public void Detach(CardInstance person)
        {
            if (person == null || person != Occupant)
                return;

            personSlot.Detach(person);
            person.GetComponent<LocationEntranceOccupant>()?.Configure(null);
            RefreshOutline();
            if (IsSelected)
                SelectionChanged?.Invoke(this);
        }

        public static bool CanAccept(CardStack droppedStack)
        {
            return droppedStack?.Cards != null && droppedStack.Cards.Count == 1 &&
                droppedStack.Cards[0]?.Definition != null &&
                droppedStack.Cards[0].Definition.Category == CardCategory.Character &&
                droppedStack.Cards[0].Definition.Faction == CardFaction.Player;
        }

        public static LocationEntrance FindNearby(CardInstance droppedCard, float searchRadius)
        {
            if (droppedCard == null || !CanAccept(droppedCard.Stack))
                return null;

            return Physics.OverlapSphere(
                    droppedCard.transform.position,
                    Mathf.Max(0.1f, searchRadius))
                .Select(hit => hit.GetComponent<LocationEntrance>())
                .Where(entrance => entrance != null &&
                    entrance.gameObject != droppedCard.gameObject &&
                    !string.IsNullOrWhiteSpace(entrance.DestinationLocationId))
                .OrderBy(entrance =>
                    (entrance.transform.position - droppedCard.transform.position).sqrMagnitude)
                .FirstOrDefault();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (InputManager.Instance != null && !InputManager.Instance.IsInputEnabled)
                return;

            SetSelected(!IsSelected, instant: false);
        }

        public void SetSelected(bool selected, bool instant = false)
        {
            bool changed = IsSelected != selected;
            if (selected && activeSelection != null && activeSelection != this)
                activeSelection.SetSelectedInternal(false, notify: false);

            SetSelectedInternal(selected, notify: changed);
        }

        private void SetSelectedInternal(bool selected, bool notify)
        {
            IsSelected = selected;
            if (selected)
                activeSelection = this;
            else if (activeSelection == this)
                activeSelection = null;

            EnsurePersonSlot();
            if (selected)
                personSlot.ShowCards();
            else
                personSlot.HideCards();
            RefreshOutline();

            if (notify)
                SelectionChanged?.Invoke(selected ? this : null);
        }

        public static void NotifyCardClicked(CardInstance clickedCard)
        {
            if (activeSelection == null || clickedCard == null)
                return;

            if (clickedCard == activeSelection.Card)
                return;

            if (clickedCard.GetComponent<LocationEntrance>() != null)
                return;

            activeSelection.SetSelected(false, instant: false);
        }

        private void EnsurePersonSlot()
        {
            if (buildingCard == null)
                buildingCard = GetComponent<CardInstance>();

            if (personSlot == null)
                personSlot = GetComponent<WorldMapPersonSlot>();
            if (personSlot == null)
                personSlot = gameObject.AddComponent<WorldMapPersonSlot>();

            personSlot.Initialize(buildingCard);
        }

        private void RefreshOutline()
        {
            if (buildingCard == null)
                return;

            if (IsSelected)
                buildingCard.SetHighlighted(true);
            else if (Occupant != null)
                buildingCard.SetHighlighted(true, occupiedOutlineColor);
            else
                buildingCard.SetHighlighted(false);
        }

        private void OnDisable()
        {
            if (IsSelected)
                SetSelected(false, instant: true);
        }

        private void OnDestroy()
        {
            if (IsSelected)
                SetSelected(false, instant: true);
        }
    }

    [DisallowMultipleComponent]
    internal sealed class LocationEntranceOccupant : MonoBehaviour, ICardDragStartHandler
    {
        private LocationEntrance entrance;

        public void Configure(LocationEntrance owner)
        {
            entrance = owner;
        }

        public void HandleDragStarted(CardInstance card)
        {
            entrance?.Detach(card);
        }
    }
}
