using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CreateAssetMenu(menuName = "StackCraft/Recipe", fileName = "Recipe_")]
    public class RecipeDefinition : ScriptableObject
    {
        [System.Serializable]
        public struct Ingredient
        {
            public CardDefinition card;
            public int count;
            public IngredientConsumption consumptionMode;
        }

        [SerializeField, Tooltip("Unique identifier for this recipe. Automatically generated if left empty.")]
        protected string id;

        [SerializeField, Tooltip("The category this recipe belongs to in the UI.")]
        protected RecipeCategory category;

        [SerializeField, Tooltip("The user-facing name for this recipe.")]
        protected string displayName;

        [SerializeField, Tooltip("List of required card definitions and their quantities.")]
        protected List<Ingredient> requiredIngredients;

        [SerializeField, Tooltip("The card definition that is created when the recipe is fulfilled.")]
        protected CardDefinition resultingCard;

        [SerializeField, Tooltip("Mark this 'true' for recipes that should run automatically and continuously (e.g. Logging Camp + Villager).")]
        protected bool isContinuous = false;

        [SerializeField, Tooltip("If true, this recipe matches even if the stack has more items than required (e.g. Sawmill + 10 Wood).")]
        protected bool allowExcessIngredients = false;

        [SerializeField, Tooltip("The time in seconds required to complete this crafting recipe.")]
        protected float craftingDuration = 5f;

        [SerializeField, Tooltip("The relative chance this recipe is chosen when multiple recipes match. Higher values are more likely.")]
        protected float randomWeight = 1.0f;

        public string Id => id;
        public RecipeCategory Category => category;
        public string DisplayName => ChineseLocalization.RecipeName(displayName);
        public List<Ingredient> RequiredIngredients => requiredIngredients;
        public CardDefinition ResultingCard => resultingCard;
        public bool IsContinuous => isContinuous;
        public bool AllowExcessIngredients => allowExcessIngredients;
        public float CraftingDuration => craftingDuration;
        public float RandomWeight => randomWeight < 0 ? 0 : randomWeight;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                id = System.Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// The Core Logic. Override this in subclasses for complex behavior.
        /// Base behavior: Standard Crafting (Consume inputs -> Spawn Output).
        /// </summary>
        public virtual void Execute(CardStack stack)
        {
            // 1. Map the rules for easy lookup
            var rules = GetIngredientRules();

            stack.TopCard.PlayPuffParticle();
            AudioManager.Instance?.PlaySFX(AudioId.Pop);

            // 2. Consume items based on those rules
            ConsumeIngredients(stack, rules);

            // 3. Spawn the result (if any)
            if (resultingCard != null)
            {
                CardManager.Instance?.CreateCardInstance(
                    resultingCard,
                    stack.TargetPosition.Flatten(),
                    stack
                );

                CraftingManager.Instance?.NotifyCraftingFinished(resultingCard);
            }
        }

        /// <summary>
        /// Checks if this recipe actually destroys/consumes anything.
        /// Used by the Manager to prevent infinite loops on "Keep" recipes.
        /// </summary>
        public bool HasConsumableIngredients()
        {
            return requiredIngredients.Any(i =>
                i.consumptionMode == IngredientConsumption.Consume ||
                i.consumptionMode == IngredientConsumption.Destroy);
        }

        #region Subclass Helpers
        protected Dictionary<CardDefinition, Ingredient> GetIngredientRules()
        {
            // Groups by card definition to handle duplicates if necessary
            return requiredIngredients
                .GroupBy(i => i.card)
                .ToDictionary(g => g.Key, g => g.First());
        }

        protected void ConsumeIngredients(CardStack stack, Dictionary<CardDefinition, Ingredient> rules)
        {
            // Create a temporary list so we can modify the stack while iterating
            var cardsToCheck = stack.Cards.ToList();

            // Track how many we still need to process (for recipes requiring multiple of the same card)
            var remainingNeeds = requiredIngredients.ToDictionary(i => i.card, i => i.count);

            foreach (var card in cardsToCheck)
            {
                if (rules.TryGetValue(card.BaseDefinition, out var rule))
                {
                    // Only consume if we still need this ingredient type
                    if (remainingNeeds[card.BaseDefinition] > 0)
                    {
                        ApplyConsumptionRule(card, rule.consumptionMode, stack);
                        remainingNeeds[card.BaseDefinition]--;
                    }
                }
            }
        }

        protected void ApplyConsumptionRule(CardInstance card, IngredientConsumption mode, CardStack stack)
        {
            switch (mode)
            {
                case IngredientConsumption.Keep:
                    break;
                case IngredientConsumption.Consume:
                    card.Use();
                    if (card.UsesLeft <= 0) stack.DestroyCard(card);
                    break;
                case IngredientConsumption.Destroy:
                    stack.DestroyCard(card);
                    break;
            }
        }
        #endregion
    }

    public enum IngredientConsumption
    {
        /// <summary>Default. Reduces uses count or durability. Destroys if empty.</summary>
        Consume = 0,

        /// <summary>The card is required but is NOT modified (e.g., Soil, Villager).</summary>
        Keep = 1,

        /// <summary>Destroys the card instance immediately, ignoring durability/uses.</summary>
        Destroy = 2
    }

    public enum RecipeCategory
    {
        Misc,
        Gathering,      // Mining, Chopping
        Construction,   // Building
        Cooking,        // Cooking, Baking, Frying
        Forging,        // Crafting, Imbuing
        Refining,       // Smelting, Sawing
        Husbandry       // Growing, Hatching
    }
}
