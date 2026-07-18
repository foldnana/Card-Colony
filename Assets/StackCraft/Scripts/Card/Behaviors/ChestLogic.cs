using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(CardInstance))]
    public class ChestLogic : MonoBehaviour, IOnStackable, IClickable
    {
        public int StoredCoins { get; private set; }

        private CardInstance card;
        private CardDefinition currency;
        private int capacity;

        public void Initialize(CardInstance card)
        {
            if (card.Definition is ChestDefinition chestDef)
            {
                this.card = card;
                this.capacity = chestDef.Capacity;
            }

            currency = TradeManager.Instance?.CurrencyCard;

            UpdateDisplay();
        }

        public void RestoreCoins(int amount)
        {
            StoredCoins = amount;
            UpdateDisplay();
        }

        public bool OnStack(CardStack droppedStack)
        {
            if (droppedStack.TopCard != null && IsValidDeposit(droppedStack.TopCard))
            {
                card.PlayPuffParticle();
                DepositCoinStack(droppedStack);
                return true;
            }
            return false;
        }

        private bool IsValidDeposit(CardInstance card)
        {
            if (currency == null || card == null) return false;
            return card.BaseDefinition == currency;
        }

        /// <summary>
        /// Consumes all valid coins from a given stack.
        /// </summary>
        public void DepositCoinStack(CardStack coinStack)
        {
            // Create a copy to iterate safely while modifying the original list.
            var cardsToDeposit = coinStack.Cards.ToList();
            bool changed = false;

            foreach (var c in cardsToDeposit)
            {
                if (StoredCoins >= capacity) break;

                if (IsValidDeposit(c))
                {
                    StoredCoins++;
                    coinStack.DestroyCard(c);
                    changed = true;
                }
            }

            if (changed)
            {
                UpdateDisplay();
                AudioManager.Instance?.PlaySFX(AudioId.Coins);
            }

            if (coinStack != null && coinStack.Cards.Count > 0)
            {
                CardManager.Instance?.ResolveOverlaps();
            }
        }

        public bool OnClick(Vector3 clickPosition)
        {
            if (card == null) card = GetComponent<CardInstance>();

            if (card.Stack.Cards.Count == 1)
            {
                if (TryWithdrawCoin(true))
                {
                    // Use the passed-in clickPosition to reset the stack
                    card.Stack.SetTargetPosition(clickPosition);
                    CardManager.Instance?.ResolveOverlaps();
                    return true; // We handled the click
                }
            }
            return false; // We did not handle the click
        }

        /// <summary>
        /// Tries to withdraw a coin. Returns true if successful.
        /// </summary>
        /// <param name="spawnOnBoard">If true, creates a new coin card on the board. 
        /// If false, just decrements the internal count.</param>
        public bool TryWithdrawCoin(bool spawnOnBoard)
        {
            if (StoredCoins <= 0 || currency == null)
            {
                return false;
            }

            StoredCoins--;
            UpdateDisplay();
            card.PlayPuffParticle();

            if (spawnOnBoard)
            {
                Vector3 spawnPos = transform.position + new Vector3(card.Size.x, 0f);
                var finalPos = Board.Instance.EnforcePlacementRules(spawnPos, null);

                CardManager.Instance?.CreateCardInstance(currency, finalPos.Flatten(), card.Stack);
                AudioManager.Instance?.PlaySFX(AudioId.Coin);
            }

            return true;
        }

        /// <summary>
        /// Updates the card's text display.
        /// </summary>
        private void UpdateDisplay()
        {
            if (card == null) card = GetComponent<CardInstance>();
            card.UpdatePriceText(StoredCoins.ToString());
        }
    }
}
