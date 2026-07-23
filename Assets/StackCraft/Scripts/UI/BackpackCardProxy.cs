using UnityEngine;

namespace CryingSnow.StackCraft
{
    [DisallowMultipleComponent]
    public sealed class BackpackCardProxy : MonoBehaviour,
        ICardDropHandler,
        ICardDragHeightProvider,
        ICardStackRegistrationPolicy
    {
        private BackpackView owner;
        private BackpackBoardView board;

        public CardInstance Card { get; private set; }
        public string EntryId { get; private set; }
        public int SlotIndex { get; private set; }
        public bool RegisterSplitStacksWithWorld => false;

        public void Bind(
            BackpackView backpackView,
            BackpackBoardView boardView,
            CardInstance card,
            string entryId,
            int slotIndex)
        {
            owner = backpackView;
            board = boardView;
            Card = card;
            EntryId = entryId;
            SlotIndex = slotIndex;
        }

        public bool HandleDrop(CardInstance card, Vector3 dropPosition)
        {
            if (card != Card)
                return false;

            if (board != null && board.ContainsScreenPoint(Input.mousePosition))
            {
                board.PlaceOnTable(this, dropPosition);
                return true;
            }

            if (owner != null && owner.TryTakeProxyToWorld(this, dropPosition))
                return true;

            board?.ReturnToSlot(this);
            return true;
        }

        public float GetDragHeight(CardInstance card)
        {
            return board != null
                ? board.SurfaceHeight + 0.18f
                : card?.Settings?.DragHeight ?? 0.1f;
        }
    }
}
