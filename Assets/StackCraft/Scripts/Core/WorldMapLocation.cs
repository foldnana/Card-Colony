using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class WorldMapLocation : MonoBehaviour, IPointerClickHandler
    {
        private static WorldMapLocation activeSelection;

        [Header("Selection")]
        [SerializeField, Min(0f)] private float selectionLiftHeight = 0.07f;
        [SerializeField, Min(0.01f)] private float selectionMoveDuration = 0.16f;
        [SerializeField, Min(0f)] private float selectionBobHeight = 0.012f;
        [SerializeField, Min(0.01f)] private float selectionBobDuration = 0.5f;

        [Header("Party Occupancy")]
        [SerializeField] private Color occupiedOutlineColor = new Color(0.95f, 0.72f, 0.22f, 1f);

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

        public void Initialize(int index, CardInstance card)
        {
            Index = index;
            Card = card;

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
            SetSelected(false, instant: true);
        }

        private void OnDestroy()
        {
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
            if (selected && activeSelection != null && activeSelection != this)
                activeSelection.SetSelected(false, instant);

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

            AnimateSelectionPose(selected, instant);
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
                    if (IsSelected)
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

            bool shouldShowOutline = IsSelected || DockedParty != null;
            if (!shouldShowOutline)
            {
                if (locationOutlineActive)
                    Card.SetHighlighted(false);

                locationOutlineActive = false;
                return;
            }

            if (IsSelected)
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

            activeSelection.SetSelected(false, instant: false);
        }
    }
}
