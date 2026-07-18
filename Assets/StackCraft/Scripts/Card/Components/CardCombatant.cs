using UnityEngine;

namespace CryingSnow.StackCraft
{
    [RequireComponent(typeof(CardInstance))]
    public class CardCombatant : MonoBehaviour
    {
        public bool IsInCombat { get; private set; }
        public bool IsAttacking { get; private set; }
        public float ActionProgress { get; private set; }
        public CombatTask CurrentCombatTask { get; private set; }

        private CardInstance _card;
        private CardAI _aiComponent;

        private void Awake()
        {
            _card = GetComponent<CardInstance>();
            _aiComponent = GetComponent<CardAI>();
        }

        /// <summary>
        /// Puts the card into a combat state.
        /// </summary>
        public void EnterCombat(CombatTask task)
        {
            CurrentCombatTask = task;
            IsInCombat = true;

            // If this card was just dragged from a crafting stack, that task must be stopped.
            if (_card.OriginalCraftingStack != null)
            {
                CraftingManager.Instance?.StopCraftingTask(_card.OriginalCraftingStack);
                _card.OriginalCraftingStack = null;
            }

            if (_card.Stack != null)
            {
                // If the stack this card is leaving was crafting, stop the craft.
                if (_card.Stack.IsCrafting)
                {
                    CraftingManager.Instance?.StopCraftingTask(_card.Stack);
                }

                _card.Stack.RemoveCard(_card); // A card in combat doesn't belong to a world stack.
            }
            _card.Stack = null;

            CardManager.Instance.TurnOffHighlightedCards();

            // Stop AI if it exists
            if (_aiComponent != null)
            {
                _aiComponent.StopAI();
            }
        }

        /// <summary>
        /// Removes the card from its combat state.
        /// </summary>
        public void LeaveCombat()
        {
            CurrentCombatTask = null;
            IsInCombat = false;

            // Restart AI if it exists
            if (_aiComponent != null)
            {
                _aiComponent.StartAI();
            }
        }

        /// <summary>
        /// Sets the card's attacking flag (used for animations/logic).
        /// </summary>
        public void SetAttackingState(bool value)
        {
            IsAttacking = value;
        }

        /// <summary>
        /// Initializes the action progress, often with a random start.
        /// </summary>
        public void InitializeCombatActionProgress()
        {
            ActionProgress = Random.Range(0f, 0.5f);
        }

        /// <summary>
        /// Adds to the action progress, checking if the card is alive.
        /// </summary>
        public void AddActionProgress(float amount)
        {
            if (_card.CurrentHealth > 0)
            {
                ActionProgress += amount;
            }
        }

        /// <summary>
        /// Resets the action progress to zero.
        /// </summary>
        public void ResetActionProgress()
        {
            ActionProgress = 0f;
        }
    }
}
