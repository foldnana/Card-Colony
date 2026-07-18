using UnityEngine;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(CardInstance))]
    public class CardEquipment : MonoBehaviour
    {
        public bool IsEquipped { get; private set; }
        public CardInstance Equipper { get; private set; } // The Character card

        private CardInstance card; // The Equipment card

        private void Awake()
        {
            card = GetComponent<CardInstance>();
        }

        /// <summary>
        /// Called by <see cref="CardEquipper"/> when this item is successfully equipped.
        /// </summary>
        public void OnEquipped(CardInstance equipperCard)
        {
            IsEquipped = true;
            Equipper = equipperCard;

            // The card no longer needs its own world stack
            if (card.Stack != null)
            {
                CardManager.Instance.UnregisterStack(card.Stack);
                card.Stack = null;
            }
        }

        /// <summary>
        /// Called by <see cref="CardEquipper"/> when this item is unequipped.
        /// </summary>
        public void OnUnequipped()
        {
            IsEquipped = false;
            Equipper = null;
            // The CardEquipper's Unequip method is responsible for
            // creating the new stack for this card on the board.
        }
    }
}
