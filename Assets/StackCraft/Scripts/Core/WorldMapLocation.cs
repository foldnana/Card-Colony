using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CryingSnow.StackCraft
{
    [Serializable]
    public sealed class WorldMapLocationDetails
    {
        public string locationId;
        [Tooltip("Whether this location already has a playable local map.")]
        public bool localMapImplemented;
        public string locationType = "地点";
        [Min(1)] public int dangerLevel = 1;
        public string travelTime = "1秒（临时）";
        public List<string> possibleResources = new() { "未知" };
        [Range(0f, 1f)] public float explorationProgress;
        [TextArea] public string description;

        public static WorldMapLocationDetails CreateFallback(CardDefinition definition)
        {
            return new WorldMapLocationDetails
            {
                description = definition != null ? definition.Description : "尚未记录这个地点的信息。"
            };
        }
    }

    [DisallowMultipleComponent]
    public sealed class WorldMapLocation : MonoBehaviour, IPointerClickHandler
    {
        private static WorldMapLocation activeSelection;

        public static event Action<WorldMapLocation> SelectionChanged;
        public static WorldMapLocation ActiveSelection => activeSelection;

        [Header("Selection")]
        [SerializeField, Min(0f)] private float selectionLiftHeight = 0.07f;
        [SerializeField, Min(0.01f)] private float selectionMoveDuration = 0.16f;
        [SerializeField, Min(0f)] private float selectionBobHeight = 0.012f;
        [SerializeField, Min(0.01f)] private float selectionBobDuration = 0.5f;

        [Header("Party Occupancy")]
        [SerializeField] private Color occupiedOutlineColor = new Color(0.95f, 0.72f, 0.22f, 1f);

        [Header("Travel")]
        [SerializeField] private Color travelingOutlineColor = new Color(0.22f, 0.92f, 0.38f, 1f);

        private WorldMapPersonSlot personSlot;
        private Tween selectionTween;
        private float restingLocalY;
        private bool hasRestingPose;
        private bool locationOutlineActive;
        private bool occupantOutlineActive;

        public int Index { get; private set; } = -1;
        public CardInstance Card { get; private set; }
        public CardInstance DockedParty => personSlot != null ? personSlot.Occupant : null;
        public WorldMapPersonSlot PersonSlot => personSlot;
        public bool IsSelected { get; private set; }
        public bool IsTravelHighlighted { get; private set; }
        public WorldMapLocationDetails Details { get; private set; }

        public void Initialize(int index, CardInstance card)
        {
            InitializeWithDetails(index, card, null);
        }

        public void InitializeWithDetails(
            int index,
            CardInstance card,
            WorldMapLocationDetails details)
        {
            Index = index;
            Card = card;
            Details = details ?? WorldMapLocationDetails.CreateFallback(card?.Definition);

            if (!hasRestingPose)
            {
                restingLocalY = transform.localPosition.y;
                hasRestingPose = true;
            }

            personSlot = GetComponent<WorldMapPersonSlot>();
            if (personSlot == null)
                personSlot = gameObject.AddComponent<WorldMapPersonSlot>();
            personSlot.Initialize(Card);

            if (Card?.Stack != null)
                Card.Stack.IsLocked = true;

            RefreshLocationOutline();
        }

        private void OnDisable()
        {
            SetTravelHighlighted(false, instant: true);
            SetSelected(false, instant: true);
        }

        private void OnDestroy()
        {
            SetTravelHighlighted(false, instant: true);
            SetSelected(false, instant: true);
            selectionTween?.Kill();
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
            SetSelectedInternal(selected, instant, notifySelectionChanged: true);
        }

        private void SetSelectedInternal(bool selected, bool instant, bool notifySelectionChanged)
        {
            bool selectionChanged = IsSelected != selected;
            if (selected && activeSelection != null && activeSelection != this)
                activeSelection.SetSelectedInternal(false, instant, notifySelectionChanged: false);

            IsSelected = selected;
            if (selected)
                activeSelection = this;
            else if (activeSelection == this)
                activeSelection = null;

            RefreshLocationOutline();
            SetOccupantOutline(selected);

            if (selected)
                personSlot?.ShowCards();
            else
                personSlot?.HideCards();

            AnimateSelectionPose(IsSelected || IsTravelHighlighted, instant);

            if (notifySelectionChanged && selectionChanged)
                SelectionChanged?.Invoke(selected ? this : null);
        }

        public void SetTravelHighlighted(bool highlighted, bool instant = false)
        {
            if (IsTravelHighlighted == highlighted)
                return;

            IsTravelHighlighted = highlighted;
            RefreshLocationOutline();
            AnimateSelectionPose(IsSelected || IsTravelHighlighted, instant);
        }

        public Vector3 GetPartyDockWorldPosition(Vector3 localDockPosition)
        {
            return personSlot != null
                ? personSlot.GetWorldAttachPosition(localDockPosition)
                : transform.TransformPoint(localDockPosition);
        }

        public void AttachParty(
            CardInstance partyCard,
            Vector3 localDockPosition,
            float dockedScale,
            bool instant)
        {
            if (partyCard == null || Card == null)
                return;

            if (personSlot == null)
            {
                personSlot = gameObject.AddComponent<WorldMapPersonSlot>();
                personSlot.Initialize(Card);
            }

            if (DockedParty != null && DockedParty != partyCard)
                SetOccupantOutline(false);

            personSlot.Attach(partyCard, localDockPosition, dockedScale, instant);

            RefreshLocationOutline();

            if (IsSelected)
            {
                personSlot.ShowCards();
                SetOccupantOutline(true);
            }
        }

        public void DetachParty(CardInstance partyCard)
        {
            if (DockedParty == partyCard)
                SetSelected(false, instant: true);

            personSlot?.Detach(partyCard);
            RefreshLocationOutline();
        }

        public void ReleaseDockedParty()
        {
            if (DockedParty != null)
                DetachParty(DockedParty);
        }

        public void RequestEnter()
        {
            SetSelected(true, instant: false);
            personSlot?.ShowCards();
        }

        private void AnimateSelectionPose(bool selected, bool instant)
        {
            selectionTween?.Kill();
            selectionTween = null;

            float targetY = restingLocalY + (selected ? selectionLiftHeight : 0f);
            if (instant)
            {
                Vector3 localPosition = transform.localPosition;
                localPosition.y = targetY;
                transform.localPosition = localPosition;

                if (selected)
                    StartSelectionBob(targetY);
                return;
            }

            selectionTween = transform
                .DOLocalMoveY(targetY, selectionMoveDuration)
                .SetEase(selected ? Ease.OutBack : Ease.OutQuad)
                .SetUpdate(true);

            if (selected)
            {
                selectionTween.OnComplete(() =>
                {
                    if (IsSelected || IsTravelHighlighted)
                        StartSelectionBob(targetY);
                });
            }
        }

        private void StartSelectionBob(float selectedY)
        {
            selectionTween?.Kill();
            selectionTween = transform
                .DOLocalMoveY(selectedY + selectionBobHeight, selectionBobDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private void RefreshLocationOutline()
        {
            if (Card == null)
                return;

            bool shouldShowOutline = IsTravelHighlighted || IsSelected || DockedParty != null;
            if (!shouldShowOutline)
            {
                if (locationOutlineActive)
                    Card.SetHighlighted(false);

                locationOutlineActive = false;
                return;
            }

            if (IsTravelHighlighted)
                Card.SetHighlighted(true, travelingOutlineColor);
            else if (IsSelected)
                Card.SetHighlighted(true);
            else
                Card.SetHighlighted(true, occupiedOutlineColor);

            locationOutlineActive = true;
        }

        private void SetOccupantOutline(bool active)
        {
            CardInstance occupant = DockedParty;
            if (occupant == null)
            {
                occupantOutlineActive = false;
                return;
            }

            if (occupantOutlineActive == active)
                return;

            occupant.SetHighlighted(active);
            occupantOutlineActive = active;
        }

        public static void NotifyCardClicked(CardInstance clickedCard)
        {
            if (activeSelection == null || clickedCard == null)
                return;

            if (clickedCard == activeSelection.Card)
                return;

            // Another location finishes the selection swap in its click handler.
            // Cancelling here would publish a transient null selection first.
            if (clickedCard.GetComponent<WorldMapLocation>() != null)
                return;

            activeSelection.SetSelected(false, instant: false);
        }
    }
}
